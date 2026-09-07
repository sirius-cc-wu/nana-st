#![cfg(all(target_arch = "x86_64", target_os = "linux"))]

use nana_st::compile_forth_source;
use rfopt::host::{Cell, Runtime};

const PASS_THROUGH: &str = "VARIABLE nana-input-0 VARIABLE nana-output-0 : nana-init 0 nana-output-0 ! ; : nana-scan nana-input-0 @ IF 1 ELSE 0 THEN nana-output-0 ! ;";

fn runtime_with_pass_through() -> Runtime {
    let generated = compile_forth_source(include_str!("fixtures/pass_through.st"))
        .expect("fixture should compile to Forth");
    assert_eq!(generated, PASS_THROUGH);

    let mut runtime = Runtime::new();
    runtime
        .load(&generated)
        .expect("generated source should load");
    runtime
}

fn scan_in_fresh_runtime(input_value: Cell) -> Cell {
    let runtime = runtime_with_pass_through();
    let input = runtime
        .resolve_cell("nana-input-0")
        .expect("input cell should resolve");
    let output = runtime
        .resolve_cell("nana-output-0")
        .expect("output cell should resolve");
    let init = runtime
        .resolve_word("nana-init")
        .expect("initialization word should resolve");
    let scan = runtime
        .resolve_word("nana-scan")
        .expect("scan word should resolve");

    init.invoke().expect("initialization should execute");
    input
        .write(input_value)
        .expect("input write should succeed");
    scan.invoke().expect("scan should execute");
    output.read().expect("output read should succeed")
}

#[test]
fn runs_the_boolean_pass_through_target_through_opaque_rfopt_handles() {
    let runtime = runtime_with_pass_through();
    let input = runtime
        .resolve_cell("nana-input-0")
        .expect("input cell should resolve");
    let output = runtime
        .resolve_cell("nana-output-0")
        .expect("output cell should resolve");
    let init = runtime
        .resolve_word("nana-init")
        .expect("initialization word should resolve");
    let scan = runtime
        .resolve_word("nana-scan")
        .expect("scan word should resolve");

    output.write(99).expect("output write should succeed");
    input.write(1).expect("input write should succeed");
    init.invoke().expect("initialization should execute");
    assert_eq!(input.read().expect("input read should succeed"), 1);
    assert_eq!(output.read().expect("output read should succeed"), 0);
    scan.invoke().expect("scan should execute");
    assert_eq!(output.read().expect("output read should succeed"), 1);

    input.write(0).expect("input write should succeed");
    scan.invoke().expect("scan should execute");
    assert_eq!(output.read().expect("output read should succeed"), 0);

    assert_eq!(scan_in_fresh_runtime(0), 0);
    assert_eq!(scan_in_fresh_runtime(1), 1);
    assert_eq!(scan_in_fresh_runtime(-1), 1);

    let missing_word = match runtime.resolve_word("missing-word") {
        Ok(_) => panic!("missing word should fail"),
        Err(error) => error,
    };
    assert!(missing_word.to_string().contains("missing-word"));

    let stale_word = {
        let runtime = runtime_with_pass_through();
        runtime
            .resolve_word("nana-scan")
            .expect("scan word should resolve")
    };
    let invalid_handle = stale_word
        .invoke()
        .expect_err("word handle should fail after its runtime drops");
    assert!(invalid_handle.to_string().contains("invalid word handle"));

    let mut malformed_runtime = Runtime::new();
    let load_error = malformed_runtime
        .load("VARIABLE input : broken missing ;")
        .expect_err("invalid Forth should not load");
    assert!(load_error.to_string().contains("1:25"));
}
