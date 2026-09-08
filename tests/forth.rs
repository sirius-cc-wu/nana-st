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
