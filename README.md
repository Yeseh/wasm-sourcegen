<div align="center">
  <h1><code>Wasi.SourceGenerator</code></h1>

  <p>
    <strong>
    C# Source generator for developing WASI modules and components
    </strong>
  </p>
</div>

## About
> **Note**: this project is experimental and should not be used in any serious context

[dotnet-wasi-sdk]: https://github.com/SteveSandersonMS/dotnet-wasi-sdk
[Component Model]: https://github.com/WebAssembly/component-model/blob/main/design/mvp/WIT.md

This repository contains a C# source generator for generating WASI modules from pure C# code. It does this by generating C code calling into the mono C driver.
This can can be compiled to a WASI module using the [dotnet-wasi-sdk].

In the future, this will hopefully support the [Component Model].

## Usage
The package has 2 projects.

`Wasi.SourceGenerator` is a class library holding types and functions relating to the source generator, this namespace can be used by guest modules and C# hosts.
`Wasi.SourceGenerator.Analyzers` contains the actual C# source generator.

See the SourceGenExample and SourceGenHost folders to see how to 
See: SourceGenExample

The project can be built and run by executing:
> make build
> make run

## Contributing
If you are interested in contributing, please open an issue to discuss the desired change. Proactive PR's are also very much welcomed!