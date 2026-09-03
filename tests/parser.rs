use nana_st::ast::{
    BinaryOperator, DataType, ExpressionKind, StatementKind, StorageClass, UnaryOperator,
};
use nana_st::lexer::lex;
use nana_st::parser::parse;

fn parse_source(source: &str) -> nana_st::ast::Program {
    let tokens = lex(source).expect("source should lex");
    parse(tokens).expect("source should parse")
}

#[test]
fn parses_declarations_assignments_and_if_else_statements() {
    let program = parse_source(
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
         ELSE
\
             count := count - 1;
\
         END_IF;
\
         active := count >= 1;
\
         END_PROGRAM",
    );

    assert_eq!(program.name.name, "Main");
    assert_eq!(program.declarations.len(), 3);
    assert_eq!(program.declarations[0].storage, StorageClass::Input);
    assert_eq!(program.declarations[0].data_type, DataType::Bool);
    assert_eq!(program.declarations[1].storage, StorageClass::Local);
    assert!(program.declarations[1].initializer.is_some());
    assert_eq!(program.declarations[2].storage, StorageClass::Output);

    let StatementKind::If {
        condition,
        then_body,
        else_body,
    } = &program.statements[0].kind
    else {
        panic!("first statement should be an IF");
    };
    assert!(matches!(condition.kind, ExpressionKind::Variable(_)));
    assert_eq!(then_body.len(), 1);
    assert_eq!(else_body.len(), 1);

    let StatementKind::Assignment { target, value } = &program.statements[1].kind else {
        panic!("second statement should be an assignment");
    };
    assert_eq!(target.name, "active");
    assert!(matches!(
        value.kind,
        ExpressionKind::Binary {
            operator: BinaryOperator::GreaterOrEqual,
            ..
        }
    ));
}

#[test]
fn parses_unary_and_binary_expression_precedence() {
    let program = parse_source(
        "PROGRAM Main
\
         VAR
\
             result : BOOL;
\
         END_VAR
\
         result := NOT FALSE OR 1 + 2 * 3 = 7;
\
         END_PROGRAM",
    );

    let StatementKind::Assignment { value, .. } = &program.statements[0].kind else {
        panic!("statement should be an assignment");
    };
    let ExpressionKind::Binary {
        operator: BinaryOperator::Or,
        left,
        right,
    } = &value.kind
    else {
        panic!("top-level operator should be OR");
    };
    assert!(matches!(
        left.kind,
        ExpressionKind::Unary {
            operator: UnaryOperator::Not,
            ..
        }
    ));
    assert!(matches!(
        right.kind,
        ExpressionKind::Binary {
            operator: BinaryOperator::Equal,
            ..
        }
    ));
}

#[test]
fn reports_the_location_of_malformed_declarations() {
    let tokens =
        lex("PROGRAM Main\nVAR\nbroken BOOL;\nEND_VAR\nEND_PROGRAM").expect("source should lex");

    let error = parse(tokens).expect_err("missing colon should fail");

    assert_eq!(error.span.start.line, 3);
    assert_eq!(error.span.start.column, 8);
    assert!(error.message.contains("':'"));
}

#[test]
fn rejects_source_after_the_single_program() {
    let tokens =
        lex("PROGRAM Main END_PROGRAM PROGRAM Other END_PROGRAM").expect("source should lex");

    let error = parse(tokens).expect_err("a second program should fail");

    assert!(error.message.contains("unexpected token after END_PROGRAM"));
}
