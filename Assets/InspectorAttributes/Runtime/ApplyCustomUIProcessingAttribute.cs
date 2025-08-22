using System;
using System.Diagnostics;

namespace InspectorAttributes
{
    /// <summary>
    /// When Applied, client code will be requested to provide <br/>
    /// "private partial void" method <br/>
    /// This method allows you to modify an original (default inspector root) <br/>
    /// Also, you could modify custom visual root (which contains new generated elements) <br/>
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(validOn: AttributeTargets.Class)]
    public sealed class ApplyCustomUIProcessingAttribute : Attribute { }
}
