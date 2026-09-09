use nana_st::compile_forth_source;

const PASS_THROUGH: &str = "VARIABLE nana-input-0 VARIABLE nana-output-0 : nana-init 0 nana-output-0 ! ; : nana-scan nana-input-0 @ IF 1 ELSE 0 THEN nana-output-0 ! ;";

#[test]
fn emits_the_exact_pass_through_forth_source() {
    let generated = compile_forth_source(include_str!("fixtures/pass_through.st"))
        .expect("pass-through fixture should compile to Forth");

    assert_eq!(generated, PASS_THROUGH);
}

#[test]
fn emits_not_expression() {
    let generated = compile_forth_source(
        "PROGRAM Main
         VAR_INPUT
             input : BOOL;
         END_VAR
         VAR_OUTPUT
             output : BOOL;
         END_VAR
         output := NOT input;
         END_PROGRAM",
    )
    .expect("should compile NOT expression to Forth");

    assert_eq!(
        generated,
        "VARIABLE nana-input-0 VARIABLE nana-output-0 : nana-init 0 nana-output-0 ! ; : nana-scan nana-input-0 @ 0= IF 1 ELSE 0 THEN nana-output-0 ! ;"
    );
}

#[test]
fn emits_arithmetic_expressions() {
    let generated = compile_forth_source(
        "PROGRAM Math
         VAR_INPUT
             a : INT;
             b : INT;
         END_VAR
         VAR_OUTPUT
             res : INT;
         END_VAR
         res := a * 2 + b - 1;
         END_PROGRAM",
    )
    .expect("should compile arithmetic expressions to Forth");

    assert_eq!(
        generated,
        "VARIABLE nana-input-0 VARIABLE nana-input-1 VARIABLE nana-output-0 : nana-init 0 nana-output-0 ! ; : nana-scan nana-input-0 @ 2 * nana-input-1 @ + 1 - nana-output-0 ! ;"
    );
}

#[test]
fn emits_comparisons_and_boolean_logic() {
    let generated = compile_forth_source(
        "PROGRAM Logic
         VAR_INPUT
             x : INT;
             y : INT;
         END_VAR
         VAR_OUTPUT
             flag : BOOL;
         END_VAR
         flag := (x > 10) AND (y <= 20);
         END_PROGRAM",
    )
    .expect("should compile comparisons and logic to Forth");

    assert_eq!(
        generated,
        "VARIABLE nana-input-0 VARIABLE nana-input-1 VARIABLE nana-output-0 : nana-init 0 nana-output-0 ! ; : nana-scan nana-input-0 @ 10 > nana-input-1 @ 20 > 0= AND IF 1 ELSE 0 THEN nana-output-0 ! ;"
    );
}

#[test]
fn emits_if_else_control_flow() {
    let generated = compile_forth_source(
        "PROGRAM Branch
         VAR_INPUT
             cond : BOOL;
         END_VAR
         VAR_OUTPUT
             out : INT;
         END_VAR
         IF cond THEN
             out := 100;
         ELSE
             out := 200;
         END_IF
         END_PROGRAM",
    )
    .expect("should compile IF ELSE control flow to Forth");

    assert_eq!(
        generated,
        "VARIABLE nana-input-0 VARIABLE nana-output-0 : nana-init 0 nana-output-0 ! ; : nana-scan nana-input-0 @ IF 100 nana-output-0 ! ELSE 200 nana-output-0 ! THEN ;"
    );
}

#[test]
fn emits_real_variables_arithmetic_and_comparisons() {
    let generated = compile_forth_source(
        "PROGRAM RealMath
         VAR_INPUT
             input : REAL;
         END_VAR
         VAR
             offset : REAL := 1.5;
         END_VAR
         VAR_OUTPUT
             output : REAL;
             high : BOOL;
         END_VAR
         output := input + offset;
         high := output >= 10.0;
         END_PROGRAM",
    )
    .expect("REAL program should compile to Forth");

    assert_eq!(
        generated,
        "FVARIABLE nana-input-0 FVARIABLE nana-var-offset FVARIABLE nana-output-0 VARIABLE nana-output-1 : nana-init 1.5 nana-var-offset F! 0.0 nana-output-0 F! 0 nana-output-1 ! ; : nana-scan nana-input-0 F@ nana-var-offset F@ F+ nana-output-0 F! nana-output-0 F@ 10.0 F>= IF 1 ELSE 0 THEN nana-output-1 ! ;"
    );
}

#[test]
fn emits_local_variables_and_initializers() {
    let generated = compile_forth_source(
        "PROGRAM State
         VAR
             counter : INT := 42;
             active : BOOL := TRUE;
         END_VAR
         VAR_OUTPUT
             out : INT;
         END_VAR
         counter := counter + 1;
         out := counter;
         END_PROGRAM",
    )
    .expect("should compile local variables and initializers to Forth");

    assert_eq!(
        generated,
        "VARIABLE nana-var-counter VARIABLE nana-var-active VARIABLE nana-output-0 : nana-init 42 nana-var-counter ! 1 nana-var-active ! 0 nana-output-0 ! ; : nana-scan nana-var-counter @ 1 + nana-var-counter ! nana-var-counter @ nana-output-0 ! ;"
    );
}

#[test]
fn emits_chiller_control_fixture() {
    let generated = compile_forth_source(include_str!("fixtures/chiller.st"))
        .expect("chiller fixture should compile to Forth");

    assert_eq!(
        generated,
        "FVARIABLE nana-input-0 FVARIABLE nana-input-1 FVARIABLE nana-input-2 VARIABLE nana-output-0 VARIABLE nana-output-1 VARIABLE nana-output-2 : nana-init 0 nana-output-0 ! 0 nana-output-1 ! 0 nana-output-2 ! ; : nana-scan 1 IF 1 ELSE 0 THEN nana-output-0 ! nana-input-0 F@ nana-input-1 F@ F> IF 1 IF 1 ELSE 0 THEN nana-output-1 ! 1 IF 1 ELSE 0 THEN nana-output-2 ! THEN nana-input-0 F@ nana-input-2 F@ F< IF 0 IF 1 ELSE 0 THEN nana-output-1 ! 0 IF 1 ELSE 0 THEN nana-output-2 ! THEN ;"
    );
}

#[test]
fn emits_dosing_control_fixture() {
    let generated = compile_forth_source(include_str!("fixtures/dosing.st"))
        .expect("dosing fixture should compile to Forth");

    assert_eq!(
        generated,
        "VARIABLE nana-input-0 FVARIABLE nana-input-1 FVARIABLE nana-input-2 FVARIABLE nana-input-3 FVARIABLE nana-input-4 FVARIABLE nana-input-5 FVARIABLE nana-input-6 VARIABLE nana-output-0 VARIABLE nana-output-1 VARIABLE nana-output-2 : nana-init 0 nana-output-0 ! 0 nana-output-1 ! 0 nana-output-2 ! ; : nana-scan nana-input-0 @ 0 = IF 0 IF 1 ELSE 0 THEN nana-output-0 ! 0 IF 1 ELSE 0 THEN nana-output-1 ! 0 IF 1 ELSE 0 THEN nana-output-2 ! nana-input-1 F@ nana-input-2 F@ F> IF 1 IF 1 ELSE 0 THEN nana-output-0 ! THEN nana-input-1 F@ nana-input-3 F@ F< IF 1 IF 1 ELSE 0 THEN nana-output-1 ! THEN nana-input-4 F@ nana-input-6 F@ F< IF 1 IF 1 ELSE 0 THEN nana-output-2 ! THEN nana-input-4 F@ nana-input-5 F@ F> IF 0 IF 1 ELSE 0 THEN nana-output-2 ! THEN THEN ;"
    );
}

#[test]
fn emits_r_trig_and_f_trig_forth() {
    let generated = compile_forth_source(
        "PROGRAM Edges
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
    .expect("edge triggers should compile to Forth");

    assert_eq!(
        generated,
        "VARIABLE nana-input-0 VARIABLE nana-var-rise-m VARIABLE nana-var-rise-q VARIABLE nana-var-fall-m VARIABLE nana-var-fall-q VARIABLE nana-output-0 VARIABLE nana-output-1 : nana-init 0 nana-var-rise-m ! 0 nana-var-rise-q ! 0 nana-var-fall-m ! 0 nana-var-fall-q ! 0 nana-output-0 ! 0 nana-output-1 ! ; : nana-scan nana-input-0 @ IF nana-var-rise-m @ 0= IF 1 ELSE 0 THEN nana-var-rise-q ! 1 nana-var-rise-m ! ELSE 0 nana-var-rise-q ! 0 nana-var-rise-m ! THEN nana-input-0 @ IF 0 nana-var-fall-q ! 1 nana-var-fall-m ! ELSE nana-var-fall-m @ 1 = IF 1 ELSE 0 THEN nana-var-fall-q ! 0 nana-var-fall-m ! THEN nana-var-rise-q @ IF 1 ELSE 0 THEN nana-output-0 ! nana-var-fall-q @ IF 1 ELSE 0 THEN nana-output-1 ! ;"
    );
}

#[test]
fn emits_ton_and_tof_with_cycle_dt_forth() {
    let generated = compile_forth_source(
        "PROGRAM Timers
VAR_INPUT
    cycle_dt : DINT;
    in_sig : BOOL;
END_VAR
VAR
    on_delay : TON;
    off_delay : TOF;
END_VAR
VAR_OUTPUT
    ton_q : BOOL;
    tof_q : BOOL;
    ton_et : DINT;
END_VAR
    on_delay(IN := in_sig, PT := 200);
    off_delay(IN := in_sig, PT := 300);
    ton_q := on_delay.Q;
    tof_q := off_delay.Q;
    ton_et := on_delay.ET;
END_PROGRAM",
    )
    .expect("timers should compile to Forth");

    assert_eq!(
        generated,
        "VARIABLE nana-input-0 VARIABLE nana-input-1 VARIABLE nana-var-on_delay-et VARIABLE nana-var-on_delay-pt VARIABLE nana-var-on_delay-q VARIABLE nana-var-off_delay-et VARIABLE nana-var-off_delay-pt VARIABLE nana-var-off_delay-q VARIABLE nana-output-0 VARIABLE nana-output-1 VARIABLE nana-output-2 : nana-init 0 nana-var-on_delay-et ! 0 nana-var-on_delay-pt ! 0 nana-var-on_delay-q ! 0 nana-var-off_delay-et ! 0 nana-var-off_delay-pt ! 0 nana-var-off_delay-q ! 0 nana-output-0 ! 0 nana-output-1 ! 0 nana-output-2 ! ; : nana-scan 200 nana-var-on_delay-pt ! nana-input-1 @ IF nana-var-on_delay-et @ nana-var-on_delay-pt @ < IF nana-var-on_delay-et @ nana-input-0 @ + nana-var-on_delay-et ! nana-var-on_delay-et @ nana-var-on_delay-pt @ > IF nana-var-on_delay-pt @ nana-var-on_delay-et ! THEN THEN nana-var-on_delay-et @ nana-var-on_delay-pt @ < 0= IF 1 ELSE 0 THEN nana-var-on_delay-q ! ELSE 0 nana-var-on_delay-et ! 0 nana-var-on_delay-q ! THEN 300 nana-var-off_delay-pt ! nana-input-1 @ IF 1 nana-var-off_delay-q ! 0 nana-var-off_delay-et ! ELSE nana-var-off_delay-q @ IF nana-var-off_delay-et @ nana-var-off_delay-pt @ < IF nana-var-off_delay-et @ nana-input-0 @ + nana-var-off_delay-et ! nana-var-off_delay-et @ nana-var-off_delay-pt @ > IF nana-var-off_delay-pt @ nana-var-off_delay-et ! THEN THEN nana-var-off_delay-et @ nana-var-off_delay-pt @ < 0= IF 0 nana-var-off_delay-q ! THEN THEN THEN nana-var-on_delay-q @ IF 1 ELSE 0 THEN nana-output-0 ! nana-var-off_delay-q @ IF 1 ELSE 0 THEN nana-output-1 ! nana-var-on_delay-et @ nana-output-2 ! ;"
    );
}
