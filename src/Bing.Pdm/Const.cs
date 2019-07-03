namespace Bing.Pdm
{
    /// <summary>
    /// 常量
    /// </summary>
    internal static class Const
    {
        #region Common

        public const string Id = "Id";

        public const string Ref = "Ref";

        public const string A = "attribute";

        public const string C = "collection";

        public const string O = "object";

        public const string AObjectId = "a:ObjectID";

        public const string AName = "a:Name";

        public const string ACode = "a:Code";

        public const string ACreationDate = "a:CreationDate";

        public const string ACreator = "a:Creator";

        public const string AModificationDate = "a:ModificationDate";

        public const string AModifier = "a:Modifier";

        public const string AComment = "a:Comment";

        public const string ADescription = "a:Description";

        #endregion

        #region Root

        public const string OModel = "o:Model";

        public const string CChildren = "c:Children";

        #endregion

        #region Model

        public const string AModelOptionsText = "a:ModelOptionsText";

        public const string AAuthor = "a:Author";

        public const string AVersion = "a:Version";

        public const string ARepositoryFilename = "a:RepositoryFilename";

        // ReSharper disable once InconsistentNaming
        public const string CDBMS = "c:DBMS";

        public const string CPhysicalDiagrams = "c:PhysicalDiagrams";

        public const string CPackages = "c:Packages";

        public const string CDefaultDiagarm = "c:DefaultDiagarm";

        public const string CUsers = "c:Users";

        public const string CDefaultGroups = "c:DefaultGroups";

        public const string CTargetModels = "c:TargetModels";

        #endregion

        #region DBMS

        public const string OShortcut = "o:Shortcut";

        public const string ATargetId = "a:TargetID";

        public const string ATargetClassId = "a:TargetClassID";

        #endregion

        #region PhysicalDiagrams

        public const string OPhysicalDiagram = "o:PhysicalDiagram";

        public const string ADisplayPreferences = "a:DisplayPreferences";

        public const string APaperSize = "a:PaperSize";

        public const string APageMargins = "a:PageMargins";

        public const string APageOrientation = "a:PageOrientation";

        public const string APaperSource = "a:PaperSource";

        public const string CSymbols = "c:Symbols";

        #endregion

        #region Symbols-PackageSymbol

        public const string OPackageSymbol = "o:PackageSymbol";

        public const string AIconMode = "a:IconMode";

        public const string ARect = "a:Rect";

        public const string ALineColor = "a:LineColor";

        public const string AFillColor = "a:FillColor";

        public const string AShadowColor = "a:ShadowColor";

        public const string AFontList = "a:FontList";

        public const string ABrushStyle = "a:BrushStyle";

        public const string AGradientFillMode = "a:GradientFillMode";

        public const string AGradientEndColor = "a:GradientEndColor";

        public const string CObject = "c:Object";

        #endregion

        #region Packages

        public const string OPackage = "o:Package";

        public const string APackageOptionsText = "a:PackageOptionsText";

        public const string CTables = "c:Tables";

        public const string CReferences = "c:References";

        #endregion

        #region Symbols-ReferenceSymbol

        public const string OReferenceSymbol = "o:ReferenceSymbol";

        public const string CSourceSymbol = "c:SourceSymbol";

        public const string CDestinationSymbol = "c:DestinationSymbol";

        #endregion

        #region Symbols-TableSymbol

        public const string OTableSymbol = "o:TableSymbol";

        #endregion

        #region Tables

        public const string OTable = "o:Table";

        public const string CColumns = "c:Columns";

        public const string CKeys = "c:Keys";

        public const string COwner = "c:Owner";

        public const string CPrimaryKey = "c:PrimaryKey";

        public const string CClusterObject = "c:ClusterObject";

        #endregion

        #region Columns

        public const string OColumn = "o:Column";

        public const string ADataType = "a:DataType";

        public const string AColumnMandatory = "a:Column.Mandatory";

        public const string APrecision = "a:Precision";

        public const string ALength = "a:Length";

        public const string AIdentity = "a:Identity";

        #endregion

        #region Keys

        public const string OKey = "o:Key";

        public const string CKeyColumns = "c:Key.Columns";

        #endregion

        #region References

        public const string OReference = "o:Reference";

        public const string ACardinality = "a:Cardinality";

        public const string CParentTable = "c:ParentTable";

        public const string CChildTable = "c:ChildTable";

        public const string CParentKey = "c:ParentKey";

        public const string CJoins = "c:Joins";

        #endregion

        #region Joins

        public const string OReferenceJoin = "o:ReferenceJoin";

        public const string CObject1 = "c:Object1";

        public const string CObject2 = "c:Object2";

        #endregion

        #region Users

        public const string OUser = "o:User";

        public const string AStereotype = "a:Stereotype";

        #endregion

        #region DefaultGroups

        public const string OGroup = "o:Group";

        public const string CGroupUsers = "c:Group.Users";

        #endregion

        #region TargetModels

        public const string OTargetModel = "o:TargetModel";

        public const string ATargetModelUrl = "a:TargetModelURL";

        public const string ATargetModelId = "a:TargetModelID";

        public const string ATargetModelClassId = "a:TargetModelClassID";

        public const string ATargetModelLastModificationDate = "a:TargetModelLastModificationDate";

        public const string CSessionShortcuts = "c:SessionShortcuts";

        #endregion

        #region Views

        public const string CViews = "c:Views";

        public const string OView = "o:View";

        // ReSharper disable once InconsistentNaming
        public const string AViewSQLQuery = "a:View.SQLQuery";

        // ReSharper disable once InconsistentNaming
        public const string ATaggedSQLQuery = "a:TaggedSQLQuery";

        #endregion

        public const string CClasses = "c:Classes";

        public const string OClass = "o:Class";

        public const string CAttributes = "c:Attributes";

        public const string OAttribute = "o:Attribute";

        public const string CIndexes = "c:Indexes";

        public const string OIndex = "o:Index";

        public const string APhysicalOptions = "a:PhysicalOptions";

        public const string AExtendedAttributesText = "a:ExtendedAttributesText";
    }
}
