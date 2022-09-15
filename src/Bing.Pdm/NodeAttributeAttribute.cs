using System;

namespace Bing.Pdm
{
    /// <summary>
    /// 节点属性。指定属性值从节点属性中获取，如果不指定此属性，将从与属性同名的子节点中获取
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class NodeAttributeAttribute : Attribute
    {
        /// <summary>
        /// 属性名
        /// </summary>
        public string AttributeName { get; }

        /// <summary>
        /// 初始化一个<see cref="NodeAttributeAttribute"/>类型的实例
        /// </summary>
        public NodeAttributeAttribute() : this(string.Empty)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="NodeAttributeAttribute"/>类型的实例
        /// </summary>
        /// <param name="attributeName">属性名</param>
        public NodeAttributeAttribute(string attributeName) => AttributeName = attributeName;
    }

    /// <summary>
    /// 节点别名属性。用于设置属性名与节点名称不一致时
    /// </summary>
    public class NodeAliasAttribute : Attribute
    {
        /// <summary>
        /// 节点名称
        /// </summary>
        public string NodeName { get; set; }

        /// <summary>
        /// 初始化一个<see cref="NodeAliasAttribute"/>类型的实例
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        public NodeAliasAttribute(string nodeName) => NodeName = nodeName;
    }

    /// <summary>
    /// 子节点属性。指定属性值从某一子节点中获取，如果不指定此属性，将从与属性同名的子节点中获取
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class NodeChildAttribute : Attribute
    {
        /// <summary>
        /// 子节点名称
        /// </summary>
        public string ChildNodeName { get; }

        /// <summary>
        /// 初始化一个<see cref="NodeChildAttribute"/>类型的实例
        /// </summary>
        /// <param name="childNodeName">子节点名称</param>
        public NodeChildAttribute(string childNodeName) => ChildNodeName = childNodeName;
    }

    /// <summary>
    /// 非基元类型属性。指定赋值的子节点
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ChildObjectAttribute : Attribute
    {
        /// <summary>
        /// 子节点名称
        /// </summary>
        public string ChildNodeName { get; }

        /// <summary>
        /// 元素类型。属性为集合类型需要指定元素类型
        /// </summary>
        public Type ElementType { get; }

        /// <summary>
        /// 初始化一个<see cref="ChildObjectAttribute"/>类型的实例
        /// </summary>
        /// <param name="childNodeName">子节点名称</param>
        public ChildObjectAttribute(string childNodeName) : this(childNodeName, null)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="ChildObjectAttribute"/>类型的实例
        /// </summary>
        /// <param name="childNodeName">子节点名称</param>
        /// <param name="elementType">元素类型</param>
        public ChildObjectAttribute(string childNodeName, Type elementType)
        {
            ChildNodeName = childNodeName;
            ElementType = elementType;
        }
    }
}