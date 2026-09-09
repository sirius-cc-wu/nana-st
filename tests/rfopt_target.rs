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
fn runs_real_arithmetic_and_branches_through_opaque_float_handles() {
    let generated = compile_forth_source(include_str!("fixtures/real_branch.st"))
        .expect("REAL fixture should compile to Forth");
    let mut runtime = Runtime::new();
    runtime
        .load(&generated)
        .expect("generated REAL source should load into rfopt runtime");

    let input = runtime.resolve_float_cell("nana-input-0").unwrap();
    let output = runtime.resolve_float_cell("nana-output-0").unwrap();
    let high = runtime.resolve_cell("nana-output-1").unwrap();
    let init = runtime.resolve_word("nana-init").unwrap();
    let scan = runtime.resolve_word("nana-scan").unwrap();

    init.invoke().unwrap();
    assert_eq!(output.read().unwrap(), 0.0);

    input.write(8.5).unwrap();
    scan.invoke().unwrap();
    assert_eq!(output.read().unwrap(), 10.0);
    assert_eq!(high.read().unwrap(), 1);

    input.write(2.0).unwrap();
    scan.invoke().unwrap();
    assert_eq!(output.read().unwrap(), 3.5);
    assert_eq!(high.read().unwrap(), 0);
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

#[test]
fn runs_chiller_decision_logic_with_hysteresis() {
    let generated = compile_forth_source(include_str!("fixtures/chiller.st"))
        .expect("chiller fixture should compile to Forth");
    let mut runtime = Runtime::new();
    runtime
        .load(&generated)
        .expect("generated chiller source should load into rfopt runtime");

    let water_temp = runtime.resolve_float_cell("nana-input-0").unwrap();
    let high_limit = runtime.resolve_float_cell("nana-input-1").unwrap();
    let low_limit = runtime.resolve_float_cell("nana-input-2").unwrap();
    let chiller_enable = runtime.resolve_cell("nana-output-0").unwrap();
    let valve_open = runtime.resolve_cell("nana-output-1").unwrap();
    let pump_run = runtime.resolve_cell("nana-output-2").unwrap();
    let init = runtime.resolve_word("nana-init").unwrap();
    let scan = runtime.resolve_word("nana-scan").unwrap();

    // 1. Initialization
    init.invoke().unwrap();
    assert_eq!(chiller_enable.read().unwrap(), 0);
    assert_eq!(valve_open.read().unwrap(), 0);
    assert_eq!(pump_run.read().unwrap(), 0);

    // Set thresholds: high_limit = 25.0, low_limit = 18.0
    high_limit.write(25.0).unwrap();
    low_limit.write(18.0).unwrap();

    // 2. Initial scan with moderate temp (within deadband: 20.0)
    water_temp.write(20.0).unwrap();
    scan.invoke().unwrap();
    assert_eq!(chiller_enable.read().unwrap(), 1);
    assert_eq!(valve_open.read().unwrap(), 0);
    assert_eq!(pump_run.read().unwrap(), 0);

    // 3. Temperature rises above high limit (26.5 > 25.0) -> chiller activates
    water_temp.write(26.5).unwrap();
    scan.invoke().unwrap();
    assert_eq!(chiller_enable.read().unwrap(), 1);
    assert_eq!(valve_open.read().unwrap(), 1);
    assert_eq!(pump_run.read().unwrap(), 1);

    // 4. Temperature drops into deadband (22.0) -> outputs remain active (hysteresis)
    water_temp.write(22.0).unwrap();
    scan.invoke().unwrap();
    assert_eq!(chiller_enable.read().unwrap(), 1);
    assert_eq!(valve_open.read().unwrap(), 1);
    assert_eq!(pump_run.read().unwrap(), 1);

    // 5. Temperature drops below low limit (17.5 < 18.0) -> chiller deactivates
    water_temp.write(17.5).unwrap();
    scan.invoke().unwrap();
    assert_eq!(chiller_enable.read().unwrap(), 1);
    assert_eq!(valve_open.read().unwrap(), 0);
    assert_eq!(pump_run.read().unwrap(), 0);

    // 6. Temperature rises back into deadband (20.0) -> outputs remain deactivated
    water_temp.write(20.0).unwrap();
    scan.invoke().unwrap();
    assert_eq!(chiller_enable.read().unwrap(), 1);
    assert_eq!(valve_open.read().unwrap(), 0);
    assert_eq!(pump_run.read().unwrap(), 0);

    // 7. Re-initialization resets all outputs
    init.invoke().unwrap();
    assert_eq!(chiller_enable.read().unwrap(), 0);
    assert_eq!(valve_open.read().unwrap(), 0);
    assert_eq!(pump_run.read().unwrap(), 0);
}

#[test]
fn runs_dosing_decision_logic() {
    let generated = compile_forth_source(include_str!("fixtures/dosing.st"))
        .expect("dosing fixture should compile to Forth");
    let mut runtime = Runtime::new();
    runtime
        .load(&generated)
        .expect("generated dosing source should load into rfopt runtime");

    let dosing_mode = runtime.resolve_cell("nana-input-0").unwrap();
    let ph_val = runtime.resolve_float_cell("nana-input-1").unwrap();
    let ph_high_limit = runtime.resolve_float_cell("nana-input-2").unwrap();
    let ph_low_limit = runtime.resolve_float_cell("nana-input-3").unwrap();
    let cond_val = runtime.resolve_float_cell("nana-input-4").unwrap();
    let cond_high_limit = runtime.resolve_float_cell("nana-input-5").unwrap();
    let cond_low_limit = runtime.resolve_float_cell("nana-input-6").unwrap();

    let hno3_pump = runtime.resolve_cell("nana-output-0").unwrap();
    let naoh_pump = runtime.resolve_cell("nana-output-1").unwrap();
    let nano3_pump = runtime.resolve_cell("nana-output-2").unwrap();

    let init = runtime.resolve_word("nana-init").unwrap();
    let scan = runtime.resolve_word("nana-scan").unwrap();

    // 1. Initialization: all outputs reset to 0
    init.invoke().unwrap();
    assert_eq!(hno3_pump.read().unwrap(), 0);
    assert_eq!(naoh_pump.read().unwrap(), 0);
    assert_eq!(nano3_pump.read().unwrap(), 0);

    // Set configuration thresholds
    ph_high_limit.write(10.0).unwrap();
    ph_low_limit.write(5.0).unwrap();
    cond_high_limit.write(100.0).unwrap();
    cond_low_limit.write(50.0).unwrap();

    // 2. Manual mode (dosing_mode = 1): pumps remain off even when out of bounds
    dosing_mode.write(1).unwrap();
    ph_val.write(12.0).unwrap();
    cond_val.write(30.0).unwrap();
    scan.invoke().unwrap();
    assert_eq!(hno3_pump.read().unwrap(), 0);
    assert_eq!(naoh_pump.read().unwrap(), 0);
    assert_eq!(nano3_pump.read().unwrap(), 0);

    // 3. Auto mode (dosing_mode = 0): High pH -> HNO3 (acid) pump activates
    dosing_mode.write(0).unwrap();
    ph_val.write(11.5).unwrap();
    cond_val.write(75.0).unwrap();
    scan.invoke().unwrap();
    assert_eq!(hno3_pump.read().unwrap(), 1);
    assert_eq!(naoh_pump.read().unwrap(), 0);
    assert_eq!(nano3_pump.read().unwrap(), 0);

    // 4. Auto mode: Normal pH (deadband) -> both pH pumps shut off
    ph_val.write(7.0).unwrap();
    scan.invoke().unwrap();
    assert_eq!(hno3_pump.read().unwrap(), 0);
    assert_eq!(naoh_pump.read().unwrap(), 0);
    assert_eq!(nano3_pump.read().unwrap(), 0);

    // 5. Auto mode: Low pH -> NaOH (base) pump activates
    ph_val.write(4.2).unwrap();
    scan.invoke().unwrap();
    assert_eq!(hno3_pump.read().unwrap(), 0);
    assert_eq!(naoh_pump.read().unwrap(), 1);
    assert_eq!(nano3_pump.read().unwrap(), 0);

    // 6. Auto mode: Low conductivity -> NaNO3 pump activates
    ph_val.write(7.0).unwrap();
    cond_val.write(40.0).unwrap();
    scan.invoke().unwrap();
    assert_eq!(hno3_pump.read().unwrap(), 0);
    assert_eq!(naoh_pump.read().unwrap(), 0);
    assert_eq!(nano3_pump.read().unwrap(), 1);

    // 7. Auto mode: High conductivity -> NaNO3 pump shuts off
    cond_val.write(120.0).unwrap();
    scan.invoke().unwrap();
    assert_eq!(hno3_pump.read().unwrap(), 0);
    assert_eq!(naoh_pump.read().unwrap(), 0);
    assert_eq!(nano3_pump.read().unwrap(), 0);

    // 8. Auto mode: Simultaneous low pH and low conductivity
    ph_val.write(4.0).unwrap();
    cond_val.write(35.0).unwrap();
    scan.invoke().unwrap();
    assert_eq!(hno3_pump.read().unwrap(), 0);
    assert_eq!(naoh_pump.read().unwrap(), 1);
    assert_eq!(nano3_pump.read().unwrap(), 1);

    // 9. Re-initialization resets all outputs
    init.invoke().unwrap();
    assert_eq!(hno3_pump.read().unwrap(), 0);
    assert_eq!(naoh_pump.read().unwrap(), 0);
    assert_eq!(nano3_pump.read().unwrap(), 0);
}

#[test]
fn runs_r_trig_and_f_trig_edge_detection() {
    let generated = compile_forth_source(
        "PROGRAM EdgeProfile
VAR_INPUT
    sig : BOOL;
END_VAR
VAR
    rise : R_TRIG;
    fall : F_TRIG;
END_VAR
VAR_OUTPUT
    rise_q : BOOL;
    fall_q : BOOL;
END_VAR
    rise(CLK := sig);
    fall(CLK := sig);
    rise_q := rise.Q;
    fall_q := fall.Q;
END_PROGRAM",
    )
    .expect("edge profile should compile to Forth");

    let mut runtime = Runtime::new();
    runtime
        .load(&generated)
        .expect("edge profile should load into rfopt runtime");

    let sig = runtime.resolve_cell("nana-input-0").unwrap();
    let rise_q = runtime.resolve_cell("nana-output-0").unwrap();
    let fall_q = runtime.resolve_cell("nana-output-1").unwrap();
    let init = runtime.resolve_word("nana-init").unwrap();
    let scan = runtime.resolve_word("nana-scan").unwrap();

    init.invoke().unwrap();
    assert_eq!(rise_q.read().unwrap(), 0);
    assert_eq!(fall_q.read().unwrap(), 0);

    // Initial low scan
    sig.write(0).unwrap();
    scan.invoke().unwrap();
    assert_eq!(rise_q.read().unwrap(), 0);
    assert_eq!(fall_q.read().unwrap(), 0);

    // Rising edge: 0 -> 1
    sig.write(1).unwrap();
    scan.invoke().unwrap();
    assert_eq!(
        rise_q.read().unwrap(),
        1,
        "R_TRIG should fire on rising edge"
    );
    assert_eq!(fall_q.read().unwrap(), 0);

    // Held high: 1 -> 1
    scan.invoke().unwrap();
    assert_eq!(
        rise_q.read().unwrap(),
        0,
        "R_TRIG should only fire for one cycle"
    );
    assert_eq!(fall_q.read().unwrap(), 0);

    // Held high again: 1 -> 1
    scan.invoke().unwrap();
    assert_eq!(rise_q.read().unwrap(), 0);
    assert_eq!(fall_q.read().unwrap(), 0);

    // Falling edge: 1 -> 0
    sig.write(0).unwrap();
    scan.invoke().unwrap();
    assert_eq!(rise_q.read().unwrap(), 0);
    assert_eq!(
        fall_q.read().unwrap(),
        1,
        "F_TRIG should fire on falling edge"
    );

    // Held low: 0 -> 0
    scan.invoke().unwrap();
    assert_eq!(rise_q.read().unwrap(), 0);
    assert_eq!(
        fall_q.read().unwrap(),
        0,
        "F_TRIG should only fire for one cycle"
    );

    // Re-initialization resets internal state
    init.invoke().unwrap();
    assert_eq!(rise_q.read().unwrap(), 0);
    assert_eq!(fall_q.read().unwrap(), 0);
}

#[test]
fn runs_ton_on_delay_with_discrete_cycle_dt() {
    let generated = compile_forth_source(
        "PROGRAM TimerProfile
VAR_INPUT
    cycle_dt : DINT;
    enable : BOOL;
END_VAR
VAR
    timer : TON;
END_VAR
VAR_OUTPUT
    out_q : BOOL;
    elapsed : DINT;
END_VAR
    timer(IN := enable, PT := 50);
    out_q := timer.Q;
    elapsed := timer.ET;
END_PROGRAM",
    )
    .expect("timer profile should compile to Forth");

    let mut runtime = Runtime::new();
    runtime
        .load(&generated)
        .expect("timer profile should load into rfopt runtime");

    let cycle_dt = runtime.resolve_cell("nana-input-0").unwrap();
    let enable = runtime.resolve_cell("nana-input-1").unwrap();
    let out_q = runtime.resolve_cell("nana-output-0").unwrap();
    let elapsed = runtime.resolve_cell("nana-output-1").unwrap();
    let init = runtime.resolve_word("nana-init").unwrap();
    let scan = runtime.resolve_word("nana-scan").unwrap();

    init.invoke().unwrap();
    cycle_dt.write(10).unwrap(); // 10 ms per scan cycle
    enable.write(0).unwrap();

    // Scan with enable = 0
    scan.invoke().unwrap();
    assert_eq!(out_q.read().unwrap(), 0);
    assert_eq!(elapsed.read().unwrap(), 0);

    // Cycle 1: enable = 1 -> ET = 10, Q = 0
    enable.write(1).unwrap();
    scan.invoke().unwrap();
    assert_eq!(elapsed.read().unwrap(), 10);
    assert_eq!(out_q.read().unwrap(), 0);

    // Cycle 2: ET = 20, Q = 0
    scan.invoke().unwrap();
    assert_eq!(elapsed.read().unwrap(), 20);
    assert_eq!(out_q.read().unwrap(), 0);

    // Cycle 3: ET = 30, Q = 0
    scan.invoke().unwrap();
    assert_eq!(elapsed.read().unwrap(), 30);
    assert_eq!(out_q.read().unwrap(), 0);

    // Cycle 4: ET = 40, Q = 0
    scan.invoke().unwrap();
    assert_eq!(elapsed.read().unwrap(), 40);
    assert_eq!(out_q.read().unwrap(), 0);

    // Cycle 5: ET = 50, Q = 1 (delay elapsed)
    scan.invoke().unwrap();
    assert_eq!(elapsed.read().unwrap(), 50);
    assert_eq!(out_q.read().unwrap(), 1);

    // Cycle 6: ET saturates at PT = 50, Q = 1
    scan.invoke().unwrap();
    assert_eq!(elapsed.read().unwrap(), 50);
    assert_eq!(out_q.read().unwrap(), 1);

    // Cycle 7: enable = 0 -> immediate reset of ET and Q
    enable.write(0).unwrap();
    scan.invoke().unwrap();
    assert_eq!(elapsed.read().unwrap(), 0);
    assert_eq!(out_q.read().unwrap(), 0);

    // Cycle 8: enable = 1 -> starts accumulating again from 0
    enable.write(1).unwrap();
    scan.invoke().unwrap();
    assert_eq!(elapsed.read().unwrap(), 10);
    assert_eq!(out_q.read().unwrap(), 0);

    // Cycle 9: interruption before PT -> drops back to 0 immediately
    enable.write(0).unwrap();
    scan.invoke().unwrap();
    assert_eq!(elapsed.read().unwrap(), 0);
    assert_eq!(out_q.read().unwrap(), 0);
}

#[test]
fn runs_tof_off_delay_with_discrete_cycle_dt() {
    let generated = compile_forth_source(
        "PROGRAM OffDelayProfile
VAR_INPUT
    cycle_dt : DINT;
    trigger : BOOL;
END_VAR
VAR
    timer : TOF;
END_VAR
VAR_OUTPUT
    out_q : BOOL;
    elapsed : DINT;
END_VAR
    timer(IN := trigger, PT := 30);
    out_q := timer.Q;
    elapsed := timer.ET;
END_PROGRAM",
    )
    .expect("off delay profile should compile to Forth");

    let mut runtime = Runtime::new();
    runtime
        .load(&generated)
        .expect("off delay profile should load into rfopt runtime");

    let cycle_dt = runtime.resolve_cell("nana-input-0").unwrap();
    let trigger = runtime.resolve_cell("nana-input-1").unwrap();
    let out_q = runtime.resolve_cell("nana-output-0").unwrap();
    let elapsed = runtime.resolve_cell("nana-output-1").unwrap();
    let init = runtime.resolve_word("nana-init").unwrap();
    let scan = runtime.resolve_word("nana-scan").unwrap();

    init.invoke().unwrap();
    cycle_dt.write(10).unwrap(); // 10 ms per scan cycle
    trigger.write(0).unwrap();

    // Startup with trigger = 0: Q must remain 0 and not spuriously activate
    scan.invoke().unwrap();
    assert_eq!(out_q.read().unwrap(), 0);
    assert_eq!(elapsed.read().unwrap(), 0);
    scan.invoke().unwrap();
    assert_eq!(out_q.read().unwrap(), 0);
    assert_eq!(elapsed.read().unwrap(), 0);

    // Turn trigger on: Q becomes 1, ET = 0
    trigger.write(1).unwrap();
    scan.invoke().unwrap();
    assert_eq!(out_q.read().unwrap(), 1);
    assert_eq!(elapsed.read().unwrap(), 0);

    // Hold trigger on: Q remains 1, ET = 0
    scan.invoke().unwrap();
    assert_eq!(out_q.read().unwrap(), 1);
    assert_eq!(elapsed.read().unwrap(), 0);

    // Trigger drops to 0: off delay begins counting
    trigger.write(0).unwrap();
    scan.invoke().unwrap();
    assert_eq!(elapsed.read().unwrap(), 10);
    assert_eq!(out_q.read().unwrap(), 1);

    scan.invoke().unwrap();
    assert_eq!(elapsed.read().unwrap(), 20);
    assert_eq!(out_q.read().unwrap(), 1);

    // Delay expired at PT = 30: Q drops to 0
    scan.invoke().unwrap();
    assert_eq!(elapsed.read().unwrap(), 30);
    assert_eq!(out_q.read().unwrap(), 0);

    // Subsequent scans with trigger = 0 remain off
    scan.invoke().unwrap();
    assert_eq!(elapsed.read().unwrap(), 30);
    assert_eq!(out_q.read().unwrap(), 0);

    // Trigger turns on again: Q becomes 1 immediately, ET resets to 0
    trigger.write(1).unwrap();
    scan.invoke().unwrap();
    assert_eq!(out_q.read().unwrap(), 1);
    assert_eq!(elapsed.read().unwrap(), 0);
}

#[test]
fn runs_ton_default_dt_without_cycle_dt_input() {
    let generated = compile_forth_source(
        "PROGRAM DefaultDt
VAR_INPUT
    in_sig : BOOL;
END_VAR
VAR
    timer : TON;
END_VAR
VAR_OUTPUT
    out_q : BOOL;
    elapsed : DINT;
END_VAR
    timer(IN := in_sig, PT := 2);
    out_q := timer.Q;
    elapsed := timer.ET;
END_PROGRAM",
    )
    .expect("default dt program should compile to Forth");

    let mut runtime = Runtime::new();
    runtime
        .load(&generated)
        .expect("default dt program should load into rfopt runtime");

    let in_sig = runtime.resolve_cell("nana-input-0").unwrap();
    let out_q = runtime.resolve_cell("nana-output-0").unwrap();
    let elapsed = runtime.resolve_cell("nana-output-1").unwrap();
    let init = runtime.resolve_word("nana-init").unwrap();
    let scan = runtime.resolve_word("nana-scan").unwrap();

    init.invoke().unwrap();
    in_sig.write(1).unwrap();

    // Scan 1: default dt = 1 -> ET = 1, Q = 0
    scan.invoke().unwrap();
    assert_eq!(elapsed.read().unwrap(), 1);
    assert_eq!(out_q.read().unwrap(), 0);

    // Scan 2: ET = 2 >= PT -> Q = 1
    scan.invoke().unwrap();
    assert_eq!(elapsed.read().unwrap(), 2);
    assert_eq!(out_q.read().unwrap(), 1);
}
