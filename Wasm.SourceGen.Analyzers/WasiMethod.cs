using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Wasm.SourceGen.Analyzers 
 {

    enum MethodType
    {
        Import,
        Export
    }

    class WasiMethod
    {
        public string Assembly { get; set; }
        public string Namespace { get; set; }
        public string Class { get; set; }
        public string Name { get; set; }
        public MethodType Type { get; set; }
        public string WasmModule { get; set; }
        public string WasmFunctionName{ get; set; }
        public string WasmNamespace { get; set; }

        public bool IsStatic { get; set; }

        public List<WasiMethodInputParameter> Params { get; set; } = new List<WasiMethodInputParameter>();

        public ITypeSymbol ReturnType { get; set; }

        public string FullyQualifiedMethodName => $"{Namespace}.{Class}::{Name}";
    }
}