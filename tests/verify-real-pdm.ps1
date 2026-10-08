[CmdletBinding()]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]] $Inputs,

    [string] $OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'

function Fail([string] $Message) {
    Write-Error $Message
    exit 1
}

function Invoke-Dotnet([string[]] $Arguments, [string] $WorkingDirectory) {
    Write-Host ("> dotnet " + ($Arguments -join ' '))
    $text = (& dotnet @Arguments 2>&1 | Out-String)
    $code = $LASTEXITCODE
    if ($code -ne 0) {
        if ($text.Trim()) { Write-Host $text.TrimEnd() }
        throw "dotnet exited with code $code."
    }
    return $text
}

function Invoke-DotnetResult([string[]] $Arguments, [string] $WorkingDirectory) {
    Write-Host ("> dotnet " + ($Arguments -join ' '))
    $text = (& dotnet @Arguments 2>&1 | Out-String)
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = $text
    }
}

function Resolve-ModelName([string] $Path) {
    $name = [IO.Path]::GetFileNameWithoutExtension($Path)
    if ($name -match 'EDI|\u7535\u5b50') { return 'EDI' }
    if ($name -match '\u7269\u6d41') { return 'Logistics' }
    if ($name -match '\u4ed3\u50a8') { return 'Warehouse' }
    if ($name -match '\u8fdb\u9500\u5b58') { return 'Inventory' }
    return $null
}

function Assert-Equal([object] $Actual, [object] $Expected, [string] $Message) {
    if ($Actual -ne $Expected) { throw "$Message. Expected '$Expected', actual '$Actual'." }
}

function Get-PackageItems([object[]] $Packages, [string] $PropertyName) {
    foreach ($package in @($Packages)) {
        if ($null -eq $package) { continue }
        $property = $package.PSObject.Properties[$PropertyName]
        if ($property) {
            foreach ($item in @($property.Value)) { $item }
        }
        foreach ($item in @(Get-PackageItems @($package.Packages) $PropertyName)) { $item }
    }
}

function Get-DiagramSymbols([object[]] $Symbols) {
    foreach ($symbol in @($Symbols)) {
        if ($null -eq $symbol) { continue }
        $symbol
        foreach ($child in @(Get-DiagramSymbols @($symbol.SubSymbols))) { $child }
    }
}

function Get-XmlValue([System.Xml.XmlNode] $Node, [string] $Name) {
    if ($null -eq $Node) { return $null }
    foreach ($child in $Node.ChildNodes) {
        if ($child.NamespaceURI -eq 'attribute' -and $child.LocalName -eq $Name) { return $child.InnerText }
    }
    return $null
}

function Get-XmlRef([System.Xml.XmlNode] $Node, [string] $Name) {
    foreach ($collection in $Node.ChildNodes) {
        if ($collection.NamespaceURI -ne 'collection' -or $collection.LocalName -ne $Name) { continue }
        foreach ($target in $collection.ChildNodes) {
            if ($target.NodeType -eq [System.Xml.XmlNodeType]::Element -and $target.Attributes['Ref']) {
                return $target.Attributes['Ref'].Value
            }
        }
    }
    return $null
}

function Get-XmlCoordinates([string] $Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return @() }
    return @([Text.RegularExpressions.Regex]::Matches($Value, '-?\d+') | ForEach-Object { [int]$_.Value })
}

function Convert-PdmColor([string] $RawColor) {
    $value = 0
    if (-not [int]::TryParse($RawColor, [ref]$value) -or $value -lt 0 -or $value -gt 0xffffff) { return $null }
    return ('#{0:x2}{1:x2}{2:x2}' -f ($value -band 255), (($value -shr 8) -band 255), (($value -shr 16) -band 255))
}

function Assert-SourceModel([string] $InputPath, [object[]] $Tables, [object[]] $Shortcuts,
    [object[]] $References, [object[]] $Diagrams, [string] $ModelName) {
    $source = [System.Xml.XmlDocument]::new()
    $source.XmlResolver = $null
    $source.Load($InputPath)
    $modelNode = $source.SelectSingleNode('//*[local-name()="Model"]')
    if ($null -eq $modelNode) { throw "$ModelName source has no model node." }
    $sourceTables = @($modelNode.SelectNodes('./*[local-name()="Tables"]/*[local-name()="Table"] | .//*[local-name()="Package"]/*[local-name()="Tables"]/*[local-name()="Table"]'))
    $sourceShortcuts = @($modelNode.SelectNodes('./*[local-name()="Tables"]/*[local-name()="Shortcut"] | .//*[local-name()="Package"]/*[local-name()="Tables"]/*[local-name()="Shortcut"]'))
    Assert-Equal $sourceTables.Count $Tables.Count "$ModelName source table count"
    Assert-Equal $sourceShortcuts.Count $Shortcuts.Count "$ModelName source Shortcut count"

    $sourceById = @{}
    foreach ($node in $modelNode.SelectNodes('.//*[@Id]')) { $sourceById[[string]$node.Attributes['Id'].Value] = $node }
    $targets = @{}
    foreach ($table in $sourceTables) {
        $objectId = Get-XmlValue $table 'ObjectID'
        if (-not $targets.ContainsKey($objectId)) { $targets[$objectId] = @() }
        $targets[$objectId] += [string]$table.Attributes['Id'].Value
    }
    $shortcutById = @{}
    foreach ($shortcut in $Shortcuts) { $shortcutById[[string]$shortcut.Id] = $shortcut }
    foreach ($sourceShortcut in $sourceShortcuts) {
        $id = [string]$sourceShortcut.Attributes['Id'].Value
        if (-not $shortcutById.ContainsKey($id)) { throw "$ModelName source Shortcut '$id' is missing from JSON." }
        $shortcut = $shortcutById[$id]
        $targetId = Get-XmlValue $sourceShortcut 'TargetID'
        Assert-Equal $shortcut.TargetId $targetId "$ModelName Shortcut '$id' TargetID"
        Assert-Equal $shortcut.ObjectId (Get-XmlValue $sourceShortcut 'ObjectID') "$ModelName Shortcut '$id' ObjectID"
        $matches = @()
        if ($targets.ContainsKey($targetId)) { $matches = @($targets[$targetId]) }
        Assert-Equal $matches.Count 1 "$ModelName Shortcut '$id' unique target"
        Assert-Equal $shortcut.ResolvedTargetId $matches[0] "$ModelName Shortcut '$id' resolved target"
    }

    foreach ($reference in $References) {
        $sourceReference = $sourceById[[string]$reference.Id]
        if ($null -eq $sourceReference -or $sourceReference.LocalName -ne 'Reference') {
            throw "$ModelName reference '$($reference.Id)' is absent from source."
        }
        $parentRef = Get-XmlRef $sourceReference 'ParentTable'
        $childRef = Get-XmlRef $sourceReference 'ChildTable'
        Assert-Equal $reference.RawParentTableRef $parentRef "$ModelName reference '$($reference.Id)' raw parent"
        Assert-Equal $reference.RawChildTableRef $childRef "$ModelName reference '$($reference.Id)' raw child"
        $parentId = if ($shortcutById.ContainsKey([string]$parentRef)) { $shortcutById[[string]$parentRef].ResolvedTargetId } else { $parentRef }
        $childId = if ($shortcutById.ContainsKey([string]$childRef)) { $shortcutById[[string]$childRef].ResolvedTargetId } else { $childRef }
        Assert-Equal $reference.ParentTableId $parentId "$ModelName reference '$($reference.Id)' parent"
        Assert-Equal $reference.ChildTableId $childId "$ModelName reference '$($reference.Id)' child"
    }

    foreach ($diagram in $Diagrams) {
        foreach ($symbol in @(Get-DiagramSymbols @($diagram.Symbols))) {
            $sourceSymbol = $sourceById[[string]$symbol.Id]
            if ($null -eq $sourceSymbol -or $sourceSymbol.LocalName -ne $symbol.Kind) {
                throw "$ModelName symbol '$($symbol.Id)' is absent from source."
            }
            $rawRef = Get-XmlRef $sourceSymbol 'Object'
            Assert-Equal $symbol.RawObjectRef $rawRef "$ModelName symbol '$($symbol.Id)' raw object"
            $objectId = if ($shortcutById.ContainsKey([string]$rawRef)) { $shortcutById[[string]$rawRef].ResolvedTargetId } else { $rawRef }
            Assert-Equal $symbol.ObjectId $objectId "$ModelName symbol '$($symbol.Id)' object"
            $rawRect = @(Get-XmlCoordinates (Get-XmlValue $sourceSymbol 'Rect'))
            if ($rawRect.Count -eq 4) {
                if ($null -eq $symbol.Rect) { throw "$ModelName symbol '$($symbol.Id)' lost its rectangle." }
                foreach ($i in 0..3) {
                    $field = @('X1', 'Y1', 'X2', 'Y2')[$i]
                    Assert-Equal $symbol.Rect.$field $rawRect[$i] "$ModelName symbol '$($symbol.Id)' rectangle $field"
                }
            }
            $rawPoints = @(Get-XmlCoordinates (Get-XmlValue $sourceSymbol 'ListOfPoints'))
            if ($rawPoints.Count -ge 4 -and $rawPoints.Count % 2 -eq 0) {
                Assert-Equal @($symbol.Points).Count ($rawPoints.Count / 2) "$ModelName symbol '$($symbol.Id)' point count"
                for ($i = 0; $i -lt $rawPoints.Count; $i += 2) {
                    Assert-Equal $symbol.Points[$i / 2].X $rawPoints[$i] "$ModelName symbol '$($symbol.Id)' point X"
                    Assert-Equal $symbol.Points[$i / 2].Y $rawPoints[$i + 1] "$ModelName symbol '$($symbol.Id)' point Y"
                }
            }
            foreach ($colorName in @('LineColor', 'FillColor')) {
                Assert-Equal $symbol.$colorName (Convert-PdmColor (Get-XmlValue $sourceSymbol $colorName)) "$ModelName symbol '$($symbol.Id)' $colorName"
            }
        }
    }
}

function Assert-RepresentativeSvg([object[]] $Diagrams, [string[]] $SvgContents, [string] $ModelName) {
    $samples = @{}
    foreach ($diagram in $Diagrams) {
        foreach ($symbol in @(Get-DiagramSymbols @($diagram.Symbols))) {
            $eligible = ($symbol.Kind -eq 'TableSymbol' -and $null -ne $symbol.Rect) -or
                ($symbol.Kind -eq 'ReferenceSymbol' -and @($symbol.Points).Count -ge 2) -or
                ($symbol.Kind -eq 'NoteSymbol' -and $null -ne $symbol.Rect -and
                    -not [string]::IsNullOrWhiteSpace([string]$symbol.Text))
            if ($eligible -and -not $samples.ContainsKey($symbol.Kind)) {
                $samples[$symbol.Kind] = [pscustomobject]@{ Diagram = $diagram; Symbol = $symbol }
            }
        }
    }
    foreach ($kind in @('TableSymbol', 'ReferenceSymbol', 'NoteSymbol')) {
        if (-not $samples.ContainsKey($kind)) { continue }
        $diagram = $samples[$kind].Diagram
        $symbol = $samples[$kind].Symbol
        $id = [string]$symbol.Id
        $content = @($SvgContents | Where-Object { $_.Contains('data-symbol-id="' + $id + '"') } | Select-Object -First 1)
        if ($content.Count -ne 1) { throw "$ModelName $kind '$id' is missing from SVG." }
        $svg = [System.Xml.XmlDocument]::new()
        $svg.XmlResolver = $null
        $svg.LoadXml($content[0])
        $group = $svg.SelectSingleNode("//*[local-name()='g' and @data-symbol-id='$id']")
        if ($null -eq $group) { throw "$ModelName $kind '$id' has no SVG group." }
        $allSymbols = @(Get-DiagramSymbols @($diagram.Symbols))
        $rects = @($allSymbols | Where-Object { $null -ne $_.Rect } | ForEach-Object { $_.Rect })
        $points = @($allSymbols | ForEach-Object { $_.Points })
        $xs = @($rects | ForEach-Object { $_.X1; $_.X2 }) + @($points | ForEach-Object { $_.X })
        $ys = @($rects | ForEach-Object { $_.Y1; $_.Y2 }) + @($points | ForEach-Object { $_.Y })
        $minX = ($xs | Measure-Object -Minimum).Minimum
        $maxY = ($ys | Measure-Object -Maximum).Maximum
        if ($kind -eq 'ReferenceSymbol') {
            $line = $group.SelectSingleNode('./*[local-name()="polyline"]')
            if ($null -eq $line) { throw "$ModelName reference '$id' has no SVG polyline." }
            $expectedPoints = (@($symbol.Points) | ForEach-Object { "$($_.X - $minX + 240),$($maxY - $_.Y + 240)" }) -join ' '
            Assert-Equal $line.Attributes['points'].Value $expectedPoints "$ModelName reference '$id' SVG points"
            if ($symbol.LineColor) {
                Assert-Equal $line.Attributes['stroke'].Value $symbol.LineColor "$ModelName reference '$id' SVG color"
            }
        } else {
            $rect = $group.SelectSingleNode('./*[local-name()="rect"]')
            if ($null -eq $rect) { throw "$ModelName $kind '$id' has no SVG rectangle." }
            Assert-Equal $rect.Attributes['x'].Value ([Math]::Min($symbol.Rect.X1, $symbol.Rect.X2) - $minX + 240) "$ModelName $kind '$id' SVG x"
            Assert-Equal $rect.Attributes['y'].Value ($maxY - [Math]::Max($symbol.Rect.Y1, $symbol.Rect.Y2) + 240) "$ModelName $kind '$id' SVG y"
            Assert-Equal $rect.Attributes['width'].Value ([Math]::Max(1, [Math]::Abs($symbol.Rect.X2 - $symbol.Rect.X1))) "$ModelName $kind '$id' SVG width"
            Assert-Equal $rect.Attributes['height'].Value ([Math]::Max(1, [Math]::Abs($symbol.Rect.Y2 - $symbol.Rect.Y1))) "$ModelName $kind '$id' SVG height"
            if ($symbol.FillColor) { Assert-Equal $rect.Attributes['fill'].Value $symbol.FillColor "$ModelName $kind '$id' SVG fill" }
            if ($symbol.LineColor) { Assert-Equal $rect.Attributes['stroke'].Value $symbol.LineColor "$ModelName $kind '$id' SVG stroke" }
            if ($kind -eq 'NoteSymbol') {
                $line = @([string]$symbol.Text -split "\r?\n" | Where-Object { $_.Trim() } | Select-Object -First 1)
                $svgText = @($group.SelectNodes('./*[local-name()="text"]') | ForEach-Object { $_.InnerText }) -join "&#10;"
                if ($line.Count -eq 0 -or -not $svgText.Contains($line[0].Trim())) {
                    throw "$ModelName note '$id' text is missing from SVG."
                }
            }
        }
    }
}

function Contains-Text([string] $Content, [string] $Text) {
    if ([string]::IsNullOrEmpty($Text)) { return $false }
    return $Content.IndexOf($Text, [StringComparison]::Ordinal) -ge 0
}

function Assert-DiagramSymbols([object[]] $Diagrams, [string] $Html, [string[]] $SvgContents, [string] $ModelName) {
    $svgText = $SvgContents -join "`n"
    foreach ($diagram in @($Diagrams)) {
        foreach ($symbol in @(Get-DiagramSymbols @($diagram.Symbols))) {
            if ($null -ne $symbol.Rect) {
                foreach ($coordinate in @('X1', 'Y1', 'X2', 'Y2')) {
                    $property = $symbol.Rect.PSObject.Properties[$coordinate]
                    $value = if ($property) { $property.Value } else { $null }
                    $parsed = 0
                    if ($null -eq $value -or -not [int]::TryParse([string]$value, [ref]$parsed)) {
                        throw "$ModelName diagram symbol '$($symbol.Id)' has an incomplete Rect ($coordinate)."
                    }
                }
            }
            $points = @($symbol.Points)
            if ($points.Count -gt 0) {
                $coordinateCount = $points.Count * 2
                if ($points.Count -lt 2 -or $coordinateCount % 2 -ne 0) {
                    throw "$ModelName diagram symbol '$($symbol.Id)' has $($points.Count) points; expected at least two points with paired coordinates."
                }
                foreach ($point in $points) {
                    foreach ($coordinate in @('X', 'Y')) {
                        $property = $point.PSObject.Properties[$coordinate]
                        $value = if ($property) { $property.Value } else { $null }
                        $parsed = 0
                        if ($null -eq $value -or -not [int]::TryParse([string]$value, [ref]$parsed)) {
                            throw "$ModelName diagram symbol '$($symbol.Id)' has an invalid point coordinate ($coordinate)."
                        }
                    }
                }
            }
            foreach ($colorName in @('LineColor', 'FillColor')) {
                $color = [string]$symbol.$colorName
                if (-not [string]::IsNullOrWhiteSpace($color) -and $color -notmatch '^#[0-9A-Fa-f]{6}$') {
                    throw "$ModelName diagram symbol '$($symbol.Id)' has invalid $colorName '$color'."
                }
            }
            if (-not [string]::IsNullOrWhiteSpace([string]$symbol.Text)) {
                foreach ($line in ([string]$symbol.Text -split "`r?`n")) {
                    $visibleText = $line.Trim()
                    if ([string]::IsNullOrEmpty($visibleText)) { continue }
                    $encoded = [Net.WebUtility]::HtmlEncode($visibleText)
                    if (-not (Contains-Text $Html $visibleText) -and -not (Contains-Text $Html $encoded)) {
                        throw "$ModelName diagram symbol '$($symbol.Id)' text is missing from HTML output."
                    }
                    if (-not (Contains-Text $svgText $visibleText) -and -not (Contains-Text $svgText $encoded)) {
                        throw "$ModelName diagram symbol '$($symbol.Id)' text is missing from SVG output."
                    }
                }
            }
        }
    }
}

function Get-MarkdownTableColumnCount([string] $Markdown) {
    $lines = $Markdown -split "`r?`n"
    $count = 0
    for ($i = 0; $i -lt $lines.Count - 1; $i++) {
        if ($lines[$i] -notmatch '^\|\s*Column\s*\|' -or $lines[$i + 1] -notmatch '^\|\s*---') { continue }
        for ($j = $i + 2; $j -lt $lines.Count -and $lines[$j] -match '^\|'; $j++) { $count++ }
    }
    return $count
}

function Get-HtmlTableColumnCount([string] $Html) {
    $tablesStart = $Html.IndexOf('<section id="tables">', [StringComparison]::Ordinal)
    $referencesStart = $Html.IndexOf('<section id="references">', [StringComparison]::Ordinal)
    if ($tablesStart -lt 0 -or $referencesStart -le $tablesStart) { throw 'HTML tables section was not found.' }
    $tablesHtml = $Html.Substring($tablesStart, $referencesStart - $tablesStart)
    $count = 0
    foreach ($body in [Text.RegularExpressions.Regex]::Matches($tablesHtml, '(?s)<tbody>(.*?)</tbody>')) {
        $count += [Text.RegularExpressions.Regex]::Matches($body.Groups[1].Value, '<tr>').Count
    }
    return $count
}

function Get-MarkdownDiagnosticCount([string] $Markdown) {
    $start = $Markdown.IndexOf("## Diagnostics", [StringComparison]::Ordinal)
    if ($start -lt 0) { return 0 }
    return [Text.RegularExpressions.Regex]::Matches($Markdown.Substring($start), '(?m)^-\s+[^\r\n]+').Count
}

function Get-HtmlDiagnosticCount([string] $Html) {
    $start = $Html.IndexOf('<h2>Diagnostics</h2>', [StringComparison]::Ordinal)
    if ($start -lt 0) { return 0 }
    $end = $Html.IndexOf('</section>', $start, [StringComparison]::Ordinal)
    if ($end -lt 0) { $end = $Html.Length }
    return [Text.RegularExpressions.Regex]::Matches($Html.Substring($start, $end - $start), '<li><code>').Count
}

if (-not $Inputs -or $Inputs.Count -eq 0) {
    Fail 'Usage: verify-real-pdm.ps1 <four .pdm paths> or verify-real-pdm.ps1 <directory> [-OutputDirectory <path>]'
}

$paths = if ($Inputs.Count -eq 1 -and (Test-Path -LiteralPath $Inputs[0] -PathType Container)) {
    @(Get-ChildItem -LiteralPath $Inputs[0] -Filter '*.pdm' -File | Select-Object -ExpandProperty FullName)
} else {
    @($Inputs | ForEach-Object { [IO.Path]::GetFullPath($_) })
}
if ($paths.Count -ne 4) { Fail "Expected exactly four PDM files, found $($paths.Count)." }

$expected = @{
    EDI       = @{ Tables = 29;  Columns = 281;  References = 9;   Diagrams = 17;  Diagnostics = @() }
    Logistics = @{ Tables = 135; Columns = 2412; References = 78;  Diagrams = 68;  Diagnostics = @('o2884', 'o3199'); Index = @('o1315', 'o1308') }
    Warehouse = @{ Tables = 141; Columns = 2428; References = 150; Diagrams = 90; Diagnostics = @(); Index = @('o177', 'o170') }
    Inventory = @{ Tables = 294; Columns = 5891; References = 223; Diagrams = 137; Diagnostics = @('o7727', 'o7809', 'o7810') }
}

$models = @{}
foreach ($path in $paths) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Fail "PDM file does not exist: $path" }
    if ([IO.Path]::GetExtension($path) -ine '.pdm') { Fail "Input is not a .pdm file: $path" }
    $name = Resolve-ModelName $path
    if (-not $name) { Fail "Cannot classify PDM file by name: $path. Expected EDI/Logistics/Warehouse/Inventory naming." }
    if ($models.ContainsKey($name)) { Fail "Duplicate PDM model '$name'." }
    $models[$name] = $path
}
if ($models.Count -ne 4) { Fail 'The four inputs must include EDI, Logistics, Warehouse and Inventory models.' }

$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$toolProject = Join-Path $repo 'samples/Bing.Pdm.Tool/Bing.Pdm.Tool.csproj'
$toolDll = Join-Path $repo 'samples/Bing.Pdm.Tool/bin/Debug/net8.0/Bing.Pdm.Tool.dll'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path ([IO.Path]::GetTempPath()) 'Bing.Pdm.RealPdm' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$OutputDirectory = Join-Path $outputRoot ('run-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null

try {
    Invoke-Dotnet @('build', $toolProject, '--no-restore', '--nologo', '--verbosity', 'minimal') $repo | Out-Null
    if (-not (Test-Path -LiteralPath $toolDll -PathType Leaf)) { throw "CLI assembly was not produced: $toolDll" }

    foreach ($name in @('EDI', 'Logistics', 'Warehouse', 'Inventory')) {
        $input = $models[$name]
        $root = Join-Path $OutputDirectory $name
        $exports = Join-Path $root 'exports'
        $entities = Join-Path $root 'entities'
        $strictEntities = Join-Path $root 'strict-entities'
        $strict = Invoke-DotnetResult @($toolDll, 'generate', $input, $strictEntities, "${name}.Entities") $repo
        if ($strict.ExitCode -eq 0) {
            if (-not (Test-Path -LiteralPath $strictEntities -PathType Container) -or
                @(Get-ChildItem -LiteralPath $strictEntities -Filter '*.cs' -File).Count -eq 0) {
                throw "Strict generation for $name succeeded without creating entity source files."
            }
            Write-Host "Strict generation succeeded for $name."
        } else {
            if (Test-Path -LiteralPath $strictEntities) {
                throw "Strict generation for $name failed but created output: $strictEntities"
            }
            if ($strict.Output -notmatch 'TYPE_ERROR') {
                throw "Strict generation for $name failed without a TYPE_ERROR diagnostic. Output: $($strict.Output.Trim())"
            }
            Write-Host "Strict generation correctly rejected $name with type diagnostics."
        }
        New-Item -ItemType Directory -Force -Path $exports | Out-Null
        Invoke-Dotnet @($toolDll, 'export', $input, $exports, 'json,md,html,svg,xlsx,docx', 'en') $repo | Out-Null

        $base = [IO.Path]::GetFileNameWithoutExtension($input)
        foreach ($extension in @('.json', '.md', '.html', '.xlsx', '.docx')) {
            $file = Join-Path $exports ($base + $extension)
            if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing $extension export for $name`: $file" }
        }
        if (@(Get-ChildItem -LiteralPath $exports -Filter '*.svg' -File).Count -ne $expected[$name].Diagrams) { throw "SVG export count mismatch for $name." }
        $svgContents = @(Get-ChildItem -LiteralPath $exports -Filter '*.svg' -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw })

        $json = Get-Content -LiteralPath (Join-Path $exports ($base + '.json')) -Raw | ConvertFrom-Json
        $markdown = Get-Content -LiteralPath (Join-Path $exports ($base + '.md')) -Raw
        $html = Get-Content -LiteralPath (Join-Path $exports ($base + '.html')) -Raw
        $tables = @($json.Tables) + @(Get-PackageItems @($json.Packages) 'Tables')
        $references = @($json.References) + @(Get-PackageItems @($json.Packages) 'References')
        $diagrams = @($json.PhysicalDiagrams) + @(Get-PackageItems @($json.Packages) 'PhysicalDiagrams')
        $shortcuts = @($json.Shortcuts) + @(Get-PackageItems @($json.Packages) 'Shortcuts')
        $columns = @($tables | ForEach-Object { @($_.Columns) }).Count
        Assert-SourceModel $input $tables $shortcuts $references $diagrams $name
        Assert-Equal $tables.Count $expected[$name].Tables "$name table count"
        Assert-Equal $columns $expected[$name].Columns "$name column count"
        Assert-Equal $references.Count $expected[$name].References "$name reference count"
        Assert-Equal $diagrams.Count $expected[$name].Diagrams "$name diagram count"
        Assert-Equal (Get-MarkdownTableColumnCount $markdown) $columns "$name Markdown column count"
        Assert-Equal (Get-HtmlTableColumnCount $html) $columns "$name HTML column count"

        $diagnostics = @($json.Diagnostics)
        Assert-Equal $diagnostics.Count $expected[$name].Diagnostics.Count "$name diagnostic count"
        Assert-Equal (Get-MarkdownDiagnosticCount $markdown) $diagnostics.Count "$name Markdown diagnostic count"
        Assert-Equal (Get-HtmlDiagnosticCount $html) $diagnostics.Count "$name HTML diagnostic count"
        foreach ($sourceId in $expected[$name].Diagnostics) {
            if (-not ($diagnostics | Where-Object { $_.SourceId -eq $sourceId })) { throw "$name is missing expected diagnostic source '$sourceId'." }
            if ($markdown -notmatch [Text.RegularExpressions.Regex]::Escape($sourceId)) { throw "$name Markdown is missing diagnostic source '$sourceId'." }
            if ($html -notmatch [Text.RegularExpressions.Regex]::Escape($sourceId)) { throw "$name HTML is missing diagnostic source '$sourceId'." }
        }
        $resolvedShortcuts = @($shortcuts | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.ResolvedTargetId) })
        if ($resolvedShortcuts.Count -gt $shortcuts.Count) { throw "$name has more resolved Shortcuts than total Shortcuts." }
        $shortcutIds = @{}
        foreach ($shortcut in $shortcuts) {
            if (-not [string]::IsNullOrWhiteSpace([string]$shortcut.Id)) { $shortcutIds[[string]$shortcut.Id] = $true }
        }
        $shortcutDiagnostics = @($diagnostics | Where-Object { $_.Code -eq 'UNRESOLVED_SHORTCUT_TARGET' -or $_.Code -eq 'AMBIGUOUS_SHORTCUT_TARGET' })
        foreach ($diagnostic in $shortcutDiagnostics) {
            if (-not $shortcutIds.ContainsKey([string]$diagnostic.SourceId)) {
                throw "$name Shortcut diagnostic '$($diagnostic.Code)' references unknown source '$($diagnostic.SourceId)'."
            }
        }
        $specialSources = @($shortcutDiagnostics | ForEach-Object { "$($_.Code):$($_.SourceId)" })
        if ($specialSources.Count -ne @($specialSources | Sort-Object -Unique).Count) {
            throw "$name contains duplicate unresolved or ambiguous Shortcut diagnostics."
        }
        Assert-DiagramSymbols $diagrams $html $svgContents $name
        Assert-RepresentativeSvg $diagrams $svgContents $name
        if ($expected[$name].Index) {
            $indexId = $expected[$name].Index[0]
            $columnId = $expected[$name].Index[1]
            $index = @($tables | ForEach-Object { @($_.Indexes) } | Where-Object { $_.Id -eq $indexId }) | Select-Object -First 1
            if (-not $index) { throw "$name is missing expected index '$indexId'." }
            $indexColumnIds = @($index.ColumnIds) + @($index.IndexColumns | ForEach-Object { $_.ColumnId })
            if ($indexColumnIds -notcontains $columnId) { throw "$name index '$indexId' is missing column '$columnId'." }
        }

        Invoke-Dotnet @($toolDll, 'generate', $input, $entities, "${name}.Entities", '--fallback-type', 'object') $repo | Out-Null
        $project = Join-Path $entities 'Generated.csproj'
        @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
"@ | Set-Content -LiteralPath $project -Encoding utf8
        Invoke-Dotnet @('build', $project, '--nologo', '--verbosity', 'quiet') $entities | Out-Null
        Write-Host "Verified ${name}: tables=$($tables.Count), columns=$columns, refs=$($references.Count), diagrams=$($diagrams.Count), shortcuts=$($shortcuts.Count), resolvedShortcuts=$($resolvedShortcuts.Count), unresolvedShortcutDiagnostics=$(@($shortcutDiagnostics | Where-Object Code -eq 'UNRESOLVED_SHORTCUT_TARGET').Count), ambiguousShortcutDiagnostics=$(@($shortcutDiagnostics | Where-Object Code -eq 'AMBIGUOUS_SHORTCUT_TARGET').Count), diagnostics=$($diagnostics.Count)"
    }
    Write-Host "Real PDM verification passed. Outputs: $OutputDirectory"
    exit 0
} catch {
    Write-Error "Real PDM verification failed: $($_.Exception.Message)"
    Write-Error "Outputs retained at: $OutputDirectory"
    exit 1
}
