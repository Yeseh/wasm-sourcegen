using System;
using Microsoft.CodeAnalysis;

namespace Wasm.SourceGen.Analyzers
{

    public class WasmMethodParameter
    {
        public string Ident { get; set; }

        public ITypeSymbol TypeSymbol { get; set; }

        public bool IsArray => TypeSymbol.TypeKind == TypeKind.Array;
        public bool IsClass => TypeSymbol.TypeKind == TypeKind.Class;
        public bool IsStruct => TypeSymbol.TypeKind == TypeKind.Struct;
        public bool IsInterface => TypeSymbol.TypeKind == TypeKind.Interface;
        public string TypeIdent => TypeSymbol.Name.ToLowerInvariant();
        public string CIdent => this.Ident.ToLowerSnakeCase();
        // TODO: Make this nicer for when more types need cleanup/transforms
        public bool NeedsTransform => TypeIdent == "string" || IsArray;
        public bool NeedsCleanup => TypeIdent == "string";

        public string CTransform
        {
            get
            {
                var typeName = TypeIdent; 
                if (IsArray) { typeName = "array"; }
                return string.Format(Generator.CTransformTemplates[typeName], CIdent);
            }
        }

        public string CParam 
        {
            get
            {
                // TODO: This feels a little dirty, can probs find something nicer to not check the dict twice
                // Because a lot of built in types like 'string' are implemented as a class underwater
                // Not really sure how to handle this yet, but it works haha
                var typeName = TypeIdent;
                var hasCParam = Generator.CInputParam.TryGetValue(typeName, out var template);
                if (hasCParam) { return string.Format(template, CIdent); }

                if (IsArray) { typeName = "array"; }
                else if (IsClass || IsInterface) { typeName = "object"; }
                else if (IsStruct) { throw new NotSupportedException("Struct types are not supported as method parameters"); }

                hasCParam = Generator.CInputParam.TryGetValue(typeName, out template);
                if (!hasCParam) { throw new NotSupportedException($"Type {typeName} was is not supported as input parameter"); }

                var formatted = string.Format(template, CIdent);
                return formatted; 
            }
        }

        public string CCleanup
            => string.Format(Generator.CCleanupTemplates[TypeIdent], CIdent);
    }
}