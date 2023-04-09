.PHONY: run-example
run-example:
	dotnet run --project examples/host-wasmtime/host-wasmtime.csproj

.PHONY: build-all
build-all: build build-example

.PHONY: build-example
build-example: guest host

.PHONY: build
build: genlib gen

genlib:
	dotnet build Wasm.SourceGen
gen: 
	dotnet build Wasm.SourceGen.Analyzers --no-incremental
guest: 
	dotnet build examples/guest/guest.csproj --no-incremental
host:  
	dotnet build examples/host-wasmtime/host-wasmtime.csproj