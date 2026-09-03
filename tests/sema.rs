use nana_st::ast::{BinaryOperator, DataType, StorageClass};
use nana_st::lexer::lex;
use nana_st::parser::parse;
use nana_st::sema::{AnalyzedExpressionKind, AnalyzedStatementKind, IntegerSemantics, analyze};

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
fn diagnoses_out_of_range_folded_int_constants() {
    let tokens = lex("PROGRAM Main VAR value : INT; END_VAR value := 32767 + 1; END_PROGRAM")
        .expect("source should lex");
    let program = parse(tokens).expect("source should parse");

    let error = analyze(program).expect_err("out-of-range INT constant should fail");

    assert!(error.message.contains("outside the INT range"));
}

#[test]
fn rejects_nonliteral_var_initializers() {
    let tokens = lex("PROGRAM Main VAR value : INT := 1 + 2; END_VAR END_PROGRAM")
        .expect("source should lex");
    let program = parse(tokens).expect("source should parse");

    let error = analyze(program).expect_err("VAR initializers must be literals");

    assert!(error.message.contains("initializer must be a literal"));
}
