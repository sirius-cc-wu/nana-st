use nana_st::ast::{BinaryOperator, DataType, StorageClass};
use nana_st::lexer::lex;
use nana_st::parser::parse;
use nana_st::sema::{
    AnalyzedExpressionKind, AnalyzedStatementKind, AnalyzedTimerUpdate, ConstantValue,
    IntegerSemantics, analyze,
};

fn analyze_source(source: &str) -> nana_st::sema::AnalyzedProgram {
    let tokens = lex(source).expect("source should lex");
    let program = parse(tokens).expect("source should parse");
    analyze(program).expect("program should analyze")
}

#[test]
fn resolves_variables_and_assigns_bnc_io_indices() {
    let program = analyze_source(
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

    assert_eq!(program.variables.len(), 3);
    assert_eq!(program.variables[0].name, "start");
    assert_eq!(program.variables[0].storage, StorageClass::Input);
    assert_eq!(program.variables[0].io_index, Some(0));
    assert_eq!(program.variables[1].data_type, DataType::Int);
    assert_eq!(program.variables[1].io_index, None);
    assert_eq!(program.variables[2].storage, StorageClass::Output);
    assert_eq!(program.variables[2].io_index, Some(0));

    let AnalyzedStatementKind::If { then_body, .. } = &program.statements[0].kind else {
        panic!("first statement should be an IF");
    };
    let AnalyzedStatementKind::Assignment { value, .. } = &then_body[0].kind else {
        panic!("IF body should contain an assignment");
    };
    assert_eq!(value.data_type, DataType::Int);
    assert!(matches!(
        value.kind,
        AnalyzedExpressionKind::Binary {
            operator: BinaryOperator::Add,
            integer_semantics: Some(IntegerSemantics::IntWrapping),
            ..
        }
    ));

    let AnalyzedStatementKind::Assignment { value, .. } = &program.statements[1].kind else {
        panic!("second statement should be an assignment");
    };
    assert_eq!(value.data_type, DataType::Bool);
    assert!(matches!(
        value.kind,
        AnalyzedExpressionKind::Binary {
            operator: BinaryOperator::GreaterOrEqual,
            integer_semantics: None,
            ..
        }
    ));
}

#[test]
fn rejects_case_insensitive_duplicate_declarations() {
    let tokens = lex("PROGRAM Main VAR Flag : BOOL; flag : BOOL; END_VAR END_PROGRAM")
        .expect("source should lex");
    let program = parse(tokens).expect("source should parse");

    let error = analyze(program).expect_err("duplicate declarations should fail");

    assert_eq!(error.span.start.column, 31);
    assert!(error.message.contains("duplicate declaration"));
}

#[test]
fn rejects_assignments_to_unknown_variables() {
    let tokens = lex("PROGRAM Main missing := TRUE; END_PROGRAM").expect("source should lex");
    let program = parse(tokens).expect("source should parse");

    let error = analyze(program).expect_err("unknown variable should fail");

    assert!(error.message.contains("unknown variable 'missing'"));
}

#[test]
fn rejects_type_mismatches_and_unsupported_input_initializers() {
    let type_error = {
        let tokens = lex("PROGRAM Main VAR enabled : BOOL; END_VAR enabled := 1; END_PROGRAM")
            .expect("source should lex");
        analyze(parse(tokens).expect("source should parse"))
            .expect_err("Boolean assignment from integer should fail")
    };
    assert!(type_error.message.contains("expected BOOL"));

    let unsupported_error = {
        let tokens = lex("PROGRAM Main VAR_INPUT enabled : BOOL := TRUE; END_VAR END_PROGRAM")
            .expect("source should lex");
        analyze(parse(tokens).expect("source should parse"))
            .expect_err("input initializer should be unsupported")
    };
    assert!(unsupported_error.message.contains("only supported for VAR"));
}

#[test]
fn infers_int_for_a_literal_on_the_left_of_a_comparison() {
    let program = analyze_source(
        "PROGRAM Main
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
         active := 1 < count;
\
         END_PROGRAM",
    );

    let AnalyzedStatementKind::Assignment { value, .. } = &program.statements[0].kind else {
        panic!("statement should be an assignment");
    };
    assert_eq!(value.data_type, DataType::Bool);
}

#[test]
fn infers_int_for_a_constant_expression_on_the_left_of_a_comparison() {
    let program = analyze_source(
        "PROGRAM Main
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
         active := 1 + 2 < count;
\
         END_PROGRAM",
    );

    let AnalyzedStatementKind::Assignment { value, .. } = &program.statements[0].kind else {
        panic!("statement should be an assignment");
    };
    assert_eq!(value.data_type, DataType::Bool);
}

#[test]
fn permits_equality_comparisons_between_boolean_variables() {
    let program = analyze_source(
        "PROGRAM Main
\
         VAR_INPUT
\
             left : BOOL;
\
             right : BOOL;
\
         END_VAR
\
         VAR_OUTPUT
\
             result : BOOL;
\
         END_VAR
\
         result := left = right;
\
         END_PROGRAM",
    );

    let AnalyzedStatementKind::Assignment { value, .. } = &program.statements[0].kind else {
        panic!("statement should be an assignment");
    };
    assert_eq!(value.data_type, DataType::Bool);
}

#[test]
fn folds_boolean_equality_constants() {
    let program = analyze_source(
        "PROGRAM Main
\
         VAR_OUTPUT
\
             result : BOOL;
\
         END_VAR
\
         result := TRUE <> FALSE;
\
         END_PROGRAM",
    );

    let AnalyzedStatementKind::Assignment { value, .. } = &program.statements[0].kind else {
        panic!("statement should be an assignment");
    };
    assert_eq!(value.constant, Some(ConstantValue::Bool(true)));
}

#[test]
fn diagnoses_out_of_range_folded_int_constants() {
    let tokens = lex("PROGRAM Main VAR value : INT; END_VAR value := 32767 + 1; END_PROGRAM")
        .expect("source should lex");
    let program = parse(tokens).expect("source should parse");

    let error = analyze(program).expect_err("out-of-range INT constant should fail");

    assert!(error.message.contains("outside the INT range"));
}

#[test]
fn accepts_real_arithmetic_but_rejects_implicit_numeric_conversions() {
    let real_program = analyze_source(
        "PROGRAM Main
         VAR
             value : REAL := 1.5;
             negative : REAL := -0.5;
         END_VAR
         VAR_OUTPUT
             active : BOOL;
         END_VAR
         value := value + 2.5;
         active := value >= 4.0;
         END_PROGRAM",
    );
    assert_eq!(real_program.variables[0].data_type, DataType::Real);
    assert_eq!(real_program.variables[1].data_type, DataType::Real);

    let tokens = lex("PROGRAM Main
         VAR
             integer : DINT;
             floating : REAL;
         END_VAR
         floating := integer + 1.0;
         END_PROGRAM")
    .expect("source should lex");
    let error = analyze(parse(tokens).expect("source should parse"))
        .expect_err("mixed integer and REAL arithmetic should fail");
    assert!(error.message.contains("expected REAL"));

    let tokens = lex("PROGRAM Main
         VAR
             value : REAL;
         END_VAR
         IF value THEN END_IF
         END_PROGRAM")
    .expect("source should lex");
    let error = analyze(parse(tokens).expect("source should parse"))
        .expect_err("REAL IF condition should fail");
    assert!(error.message.contains("expected BOOL"));
}

#[test]
fn rejects_nonliteral_var_initializers() {
    let tokens = lex("PROGRAM Main VAR value : INT := 1 + 2; END_VAR END_PROGRAM")
        .expect("source should lex");
    let program = parse(tokens).expect("source should parse");

    let error = analyze(program).expect_err("VAR initializers must be literals");

    assert!(error.message.contains("initializer must be a literal"));
}

#[test]
fn analyzes_timer_and_trigger_instances_and_resolves_cycle_dt() {
    let program = analyze_source(
        "PROGRAM Timers
VAR_INPUT
    cycle_dt : DINT;
    btn : BOOL;
END_VAR
VAR
    trig : R_TRIG;
    tmr : TON;
END_VAR
VAR_OUTPUT
    out : BOOL;
    elapsed : DINT;
END_VAR
    trig(CLK := btn);
    tmr(IN := trig.Q, PT := 500);
    out := tmr.Q;
    elapsed := tmr.ET;
END_PROGRAM",
    );

    // Variables should include:
    // 0: cycle_dt (Input)
    // 1: btn (Input)
    // 2: trig-m (Local)
    // 3: trig-q (Local)
    // 4: tmr-et (Local)
    // 5: tmr-pt (Local)
    // 6: tmr-q (Local)
    // 7: out (Output)
    // 8: elapsed (Output)
    assert_eq!(program.variables.len(), 9);
    assert_eq!(program.variables[2].name, "trig-m");
    assert_eq!(program.variables[3].name, "trig-q");
    assert_eq!(program.variables[4].name, "tmr-et");
    assert_eq!(program.variables[5].name, "tmr-pt");
    assert_eq!(program.variables[6].name, "tmr-q");

    // Statements:
    // 0: TimerUpdate(RTrig)
    // 1: TimerUpdate(Ton)
    // 2: Assignment out := tmr.Q
    // 3: Assignment elapsed := tmr.ET
    assert_eq!(program.statements.len(), 4);
    let AnalyzedStatementKind::TimerUpdate(AnalyzedTimerUpdate::RTrig { m_var, q_var, .. }) =
        &program.statements[0].kind
    else {
        panic!("statement 0 should be RTrig update");
    };
    assert_eq!(*m_var, 2);
    assert_eq!(*q_var, 3);

    let AnalyzedStatementKind::TimerUpdate(AnalyzedTimerUpdate::Ton {
        dt_var,
        et_var,
        pt_var,
        q_var,
        ..
    }) = &program.statements[1].kind
    else {
        panic!("statement 1 should be Ton update");
    };
    assert_eq!(*dt_var, Some(0)); // bound to cycle_dt at index 0
    assert_eq!(*et_var, 4);
    assert_eq!(*pt_var, 5);
    assert_eq!(*q_var, 6);
}

#[test]
fn rejects_timer_and_trigger_declaration_errors() {
    let tokens = lex("PROGRAM Main VAR_INPUT tmr : TON; END_VAR END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(err.message.contains("only supported in VAR declarations"));

    let tokens = lex("PROGRAM Main VAR_OUTPUT trig : R_TRIG; END_VAR END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(err.message.contains("only supported in VAR declarations"));

    let tokens = lex("PROGRAM Main VAR tmr : TON := 0; END_VAR END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(err.message.contains("do not support initializers"));
}

#[test]
fn rejects_timer_and_trigger_assignment_and_mutation_errors() {
    let tokens = lex("PROGRAM Main VAR tmr : TON; END_VAR tmr := 10; END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(err.message.contains("cannot assign to instance 'tmr'"));

    let tokens = lex("PROGRAM Main VAR tmr : TON; END_VAR tmr.Q := TRUE; END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(
        err.message
            .contains("cannot assign to read-only field 'Q' of instance 'tmr'")
    );

    let tokens = lex("PROGRAM Main VAR tmr : TON; END_VAR tmr.ET := 10; END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(
        err.message
            .contains("cannot assign to read-only field 'ET' of instance 'tmr'")
    );

    let tokens =
        lex("PROGRAM Main VAR count : INT; END_VAR count.field := 10; END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(
        err.message
            .contains("cannot access field on variable 'count'")
    );

    let tokens = lex("PROGRAM Main unknown.field := 10; END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(err.message.contains("unknown variable 'unknown'"));
}

#[test]
fn rejects_invalid_timer_and_trigger_invocations() {
    let tokens = lex("PROGRAM Main VAR trig : R_TRIG; END_VAR trig(); END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(err.message.contains("missing argument 'CLK'"));

    let tokens =
        lex("PROGRAM Main VAR trig : R_TRIG; END_VAR trig(FOO := TRUE); END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(err.message.contains("unexpected argument 'FOO'"));

    let tokens = lex("PROGRAM Main VAR tmr : TON; END_VAR tmr(IN := TRUE); END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(err.message.contains("missing argument 'PT'"));

    let tokens =
        lex("PROGRAM Main VAR tmr : TON; END_VAR tmr(IN := TRUE, PT := -50); END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(err.message.contains("cannot be negative"));

    let tokens =
        lex("PROGRAM Main VAR tmr : TON; END_VAR tmr(IN := TRUE, PT := 1.5); END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(err.message.contains("expected INT or DINT"));
}

#[test]
fn rejects_invalid_field_access_on_instances() {
    let tokens =
        lex("PROGRAM Main VAR trig : R_TRIG; b : BOOL; END_VAR b := trig.ET; END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(err.message.contains("has no field 'ET'"));

    let tokens =
        lex("PROGRAM Main VAR tmr : TON; b : BOOL; END_VAR b := tmr.CLK; END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(err.message.contains("has no field 'CLK'"));

    let tokens =
        lex("PROGRAM Main VAR count : INT; b : BOOL; END_VAR b := count.Q; END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(
        err.message
            .contains("cannot access field on variable 'count'; 'count' is not an instance")
    );

    let tokens = lex("PROGRAM Main VAR b : BOOL; END_VAR b := ghost.Q; END_PROGRAM").unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(err.message.contains("unknown instance 'ghost'"));
}

#[test]
fn rejects_non_integer_cycle_dt() {
    let tokens =
        lex("PROGRAM Main VAR_INPUT cycle_dt : REAL; END_VAR VAR tmr : TON; END_VAR END_PROGRAM")
            .unwrap();
    let err = analyze(parse(tokens).unwrap()).unwrap_err();
    assert!(err.message.contains("cycle_dt input must be INT or DINT"));
}
