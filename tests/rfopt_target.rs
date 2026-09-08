#![cfg(all(
    any(target_arch = "x86_64", target_arch = "aarch64"),
    target_os = "linux"
))]

use nana_st::compile_forth_source;
use rfopt::nanast::{Cell, Runtime};

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

#[test]
fn runs_multi_cycle_counter_state_and_resets() {
    let generated = compile_forth_source(include_str!("fixtures/counter.st"))
        .expect("counter fixture should compile to Forth");
    let mut runtime = Runtime::new();
    runtime
        .load(&generated)
        .expect("generated counter source should load into rfopt runtime");

    let count = runtime
        .resolve_cell("nana-var-count")
        .expect("count cell should resolve");
    let out = runtime
        .resolve_cell("nana-output-0")
        .expect("output cell should resolve");
    let init = runtime
        .resolve_word("nana-init")
        .expect("init word should resolve");
    let scan = runtime
        .resolve_word("nana-scan")
        .expect("scan word should resolve");

    // Before init, write dirty values
    count.write(999).unwrap();
    out.write(888).unwrap();

    // After init, counter and output reset to 0
    init.invoke().unwrap();
    assert_eq!(count.read().unwrap(), 0);
    assert_eq!(out.read().unwrap(), 0);

    // Scan cycle 1: count := 0 + 1 = 1
    scan.invoke().unwrap();
    assert_eq!(count.read().unwrap(), 1);
    assert_eq!(out.read().unwrap(), 1);

    // Scan cycle 2: count := 1 + 1 = 2
    scan.invoke().unwrap();
    assert_eq!(count.read().unwrap(), 2);
    assert_eq!(out.read().unwrap(), 2);

    // Scan cycle 3: count := 2 + 1 = 3
    scan.invoke().unwrap();
    assert_eq!(count.read().unwrap(), 3);
    assert_eq!(out.read().unwrap(), 3);

    // Reset via init again
    init.invoke().unwrap();
    assert_eq!(count.read().unwrap(), 0);
    assert_eq!(out.read().unwrap(), 0);

    // Scan cycle after reset: count := 0 + 1 = 1
    scan.invoke().unwrap();
    assert_eq!(count.read().unwrap(), 1);
    assert_eq!(out.read().unwrap(), 1);
}

#[test]
fn runs_arithmetic_and_comparison_expressions() {
    let source = "PROGRAM Calc
VAR_INPUT
    a : INT;
    b : INT;
END_VAR
VAR_OUTPUT
    sum : INT;
    diff : INT;
    prod : INT;
    quot : INT;
    is_greater : BOOL;
END_VAR

sum := a + b;
diff := a - b;
prod := a * b;
quot := a / b;
is_greater := a > b;
END_PROGRAM";

    let generated = compile_forth_source(source).expect("source should compile to Forth");
    let mut runtime = Runtime::new();
    runtime
        .load(&generated)
        .expect("generated source should load into rfopt runtime");

    let a = runtime.resolve_cell("nana-input-0").unwrap();
    let b = runtime.resolve_cell("nana-input-1").unwrap();
    let sum = runtime.resolve_cell("nana-output-0").unwrap();
    let diff = runtime.resolve_cell("nana-output-1").unwrap();
    let prod = runtime.resolve_cell("nana-output-2").unwrap();
    let quot = runtime.resolve_cell("nana-output-3").unwrap();
    let is_greater = runtime.resolve_cell("nana-output-4").unwrap();
    let init = runtime.resolve_word("nana-init").unwrap();
    let scan = runtime.resolve_word("nana-scan").unwrap();

    init.invoke().unwrap();
    a.write(20).unwrap();
    b.write(6).unwrap();
    scan.invoke().unwrap();

    assert_eq!(sum.read().unwrap(), 26);
    assert_eq!(diff.read().unwrap(), 14);
    assert_eq!(prod.read().unwrap(), 120);
    assert_eq!(quot.read().unwrap(), 3);
    assert_eq!(is_greater.read().unwrap(), 1);

    // Update inputs
    a.write(5).unwrap();
    b.write(10).unwrap();
    scan.invoke().unwrap();

    assert_eq!(sum.read().unwrap(), 15);
    assert_eq!(diff.read().unwrap(), -5);
    assert_eq!(prod.read().unwrap(), 50);
    assert_eq!(quot.read().unwrap(), 0);
    assert_eq!(is_greater.read().unwrap(), 0);
}
