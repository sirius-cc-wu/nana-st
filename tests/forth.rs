use nana_st::compile_forth_source;

const PASS_THROUGH: &str = "VARIABLE nana-input-0 VARIABLE nana-output-0 : nana-init 0 nana-output-0 ! ; : nana-scan nana-input-0 @ IF 1 ELSE 0 THEN nana-output-0 ! ;";

#[test]
fn emits_the_exact_pass_through_forth_source() {
    let generated = compile_forth_source(include_str!("fixtures/pass_through.st"))
        .expect("pass-through fixture should compile to Forth");

    assert_eq!(generated, PASS_THROUGH);
}

#[test]
fn rejects_a_boolean_program_outside_the_first_forth_target_shape() {
    let error = compile_forth_source(
        "PROGRAM Main
\
         VAR_INPUT
\
             input : BOOL;
\
         END_VAR
\
         VAR_OUTPUT
\
             output : BOOL;
\
         END_VAR
\
         output := NOT input;
\
         END_PROGRAM",
    )
    .expect_err("the first Forth target supports only direct pass-through");

    assert!(error.to_string().contains("Boolean pass-through"));
}
