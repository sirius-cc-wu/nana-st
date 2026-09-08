use nana_st::ast::TokenKind;
use nana_st::lexer::lex;

#[test]
fn lexes_a_program_with_declarations_and_assignment() {
    let tokens =
        lex("PROGRAM Main\nVAR\n    enabled : BOOL;\nEND_VAR\nenabled := TRUE;\nEND_PROGRAM\n")
            .expect("source should lex");

    let kinds = tokens
        .iter()
        .map(|token| token.kind.clone())
        .collect::<Vec<_>>();

    assert_eq!(
        kinds,
        vec![
            TokenKind::Program,
            TokenKind::Identifier("Main".to_owned()),
            TokenKind::Var,
            TokenKind::Identifier("enabled".to_owned()),
            TokenKind::Colon,
            TokenKind::Bool,
            TokenKind::Semicolon,
            TokenKind::EndVar,
            TokenKind::Identifier("enabled".to_owned()),
            TokenKind::Assign,
            TokenKind::True,
            TokenKind::Semicolon,
            TokenKind::EndProgram,
        ]
    );

    let program_name_span = tokens[1].span;
    assert_eq!(program_name_span.start.line, 1);
    assert_eq!(program_name_span.start.column, 9);
    assert_eq!(program_name_span.end.line, 1);
    assert_eq!(program_name_span.end.column, 13);
}

#[test]
fn lexes_operators_and_skips_block_comments() {
    let tokens = lex("value := -42 + 7 * (3 / 1); (* scan logic *) IF value >= 1 THEN END_IF")
        .expect("source should lex");

    let kinds = tokens
        .iter()
        .map(|token| token.kind.clone())
        .collect::<Vec<_>>();

    assert_eq!(
        kinds,
        vec![
            TokenKind::Identifier("value".to_owned()),
            TokenKind::Assign,
            TokenKind::Minus,
            TokenKind::Integer("42".to_owned()),
            TokenKind::Plus,
            TokenKind::Integer("7".to_owned()),
            TokenKind::Star,
            TokenKind::LeftParen,
            TokenKind::Integer("3".to_owned()),
            TokenKind::Slash,
            TokenKind::Integer("1".to_owned()),
            TokenKind::RightParen,
            TokenKind::Semicolon,
            TokenKind::If,
            TokenKind::Identifier("value".to_owned()),
            TokenKind::GreaterOrEqual,
            TokenKind::Integer("1".to_owned()),
            TokenKind::Then,
            TokenKind::EndIf,
        ]
    );
}

#[test]
fn lexes_real_type_and_decimal_exponent_literals() {
    let tokens = lex("PROGRAM Main VAR value : REAL; END_VAR value := 1.25E-3; END_PROGRAM")
        .expect("REAL source should lex");

    assert!(tokens.iter().any(|token| token.kind == TokenKind::Real));
    assert!(
        tokens
            .iter()
            .any(|token| { token.kind == TokenKind::RealLiteral("1.25E-3".to_owned()) })
    );
}

#[test]
fn reports_the_location_of_an_invalid_character() {
    let error = lex("PROGRAM @").expect_err("invalid character should fail");

    assert_eq!(error.span.start.line, 1);
    assert_eq!(error.span.start.column, 9);
    assert!(error.message.contains("@"));
}

#[test]
fn rejects_an_unterminated_block_comment() {
    let error = lex("(* unfinished").expect_err("unterminated comment should fail");

    assert_eq!(error.span.start.line, 1);
    assert_eq!(error.span.start.column, 1);
    assert!(error.message.contains("unterminated"));
}
