use nana_st::compile_source;
use wasmtime::{Config, Engine, Module, WasmFeatures};

fn compile_to_wasm(source: &str) -> Vec<u8> {
    compile_source(source).expect("source should compile to Wasm")
}

#[test]
fn emits_a_valid_module_with_the_bnc_abi() {
    let wasm = compile_to_wasm(
        "PROGRAM Main
\
         VAR_INPUT
\
             start : BOOL;
\
         END_VAR
\
         VAR
\
             count : INT := 0;
\
         END_VAR
\
         VAR_OUTPUT
\
             active : BOOL;
\
         END_VAR
\
         IF start THEN
\
             count := count + 1;
\
         END_IF;
\
         active := count >= 1;
\
         END_PROGRAM",
    );

    let engine = Engine::default();
    let module = Module::new(&engine, wasm).expect("emitted bytes should validate");

    let imports = module
        .imports()
        .map(|import| (import.module().to_owned(), import.name().to_owned()))
        .collect::<Vec<_>>();
    assert_eq!(
        imports,
        vec![
            ("bnc".to_owned(), "read_input".to_owned()),
            ("bnc".to_owned(), "write_output".to_owned()),
        ]
    );

    let exports = module
        .exports()
        .map(|export| export.name().to_owned())
        .collect::<Vec<_>>();
    assert_eq!(
        exports,
        vec!["nana_init".to_owned(), "nana_scan".to_owned()]
    );
}

#[test]
fn validates_int_code_without_the_sign_extension_proposal() {
    let wasm = compile_to_wasm(
        "PROGRAM Main
\
         VAR
\
             count : INT := 0;
\
         END_VAR
\
         count := count + 1;
\
         END_PROGRAM",
    );

    let mut config = Config::new();
    config.wasm_features(WasmFeatures::SIGN_EXTENSION, false);
    let engine = Engine::new(&config).expect("MVP engine should initialize");

    Module::new(&engine, wasm).expect("emitted bytes should not need sign extension");
}

#[test]
fn emits_modules_without_extra_runtime_imports() {
    let wasm = compile_to_wasm("PROGRAM Main END_PROGRAM");

    let engine = Engine::default();
    let module = Module::new(&engine, wasm).expect("emitted bytes should validate");

    assert_eq!(module.imports().count(), 2);
    assert_eq!(module.exports().count(), 2);
}
