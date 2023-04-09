using System;
using System.Collections.Generic;
using System.Text;

namespace Wasi.SourceGenerator;

[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class WasiExportAttribute : Attribute
{
    public string Function { get;  }

    public WasiExportAttribute(string functionName)
    {
        this.Function = functionName;
        
        if (string.IsNullOrWhiteSpace(this.Function))
        {
            throw new ArgumentException("Function name cannot be an empty string");
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class WasiImportAttribute : Attribute
{
    public string Namespace { get; }
    
    public string Module { get; }

    public string Function { get;  }

    public WasiImportAttribute(string nameSpace, string module, string functionName)
    {
        this.Namespace = nameSpace;
        this.Module = module;
        this.Function = functionName;

        if (string.IsNullOrWhiteSpace(this.Module))
        {
            throw new ArgumentException("Module name cannot be an empty string");
        }
        
        if (string.IsNullOrWhiteSpace(this.Function))
        {
            throw new ArgumentException("Function name cannot be an empty string");
        }
    }
}
