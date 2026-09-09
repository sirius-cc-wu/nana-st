use std::error::Error;
use std::fmt;
use std::iter::Peekable;
use std::str::Chars;

use crate::ast::{Position, Span, Token, TokenKind};

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct LexError {
    pub span: Span,
    pub message: String,
}

impl fmt::Display for LexError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            formatter,
            "{} at {}:{}",
            self.message, self.span.start.line, self.span.start.column
        )
    }
}

impl Error for LexError {}

pub fn lex(source: &str) -> Result<Vec<Token>, LexError> {
    Lexer::new(source).lex()
}

struct Lexer<'source> {
    characters: Peekable<Chars<'source>>,
    position: Position,
}

impl<'source> Lexer<'source> {
    fn new(source: &'source str) -> Self {
        Self {
            characters: source.chars().peekable(),
            position: Position::start(),
        }
    }

    fn lex(mut self) -> Result<Vec<Token>, LexError> {
        let mut tokens = Vec::new();

        while let Some(character) = self.peek() {
            if character.is_whitespace() {
                self.next();
                continue;
            }

            let start = self.position;
            let kind = match character {
                character if is_identifier_start(character) => self.lex_identifier_or_keyword(),
                character if character.is_ascii_digit() => self.lex_number(start)?,
                '(' if self.peek_next_is('*') => {
                    self.skip_block_comment(start)?;
                    continue;
                }
                ':' => self.one_or_two_character_token(TokenKind::Colon, '=', TokenKind::Assign),
                '<' => self.less_than_token(),
                '>' => self.one_or_two_character_token(
                    TokenKind::Greater,
                    '=',
                    TokenKind::GreaterOrEqual,
                ),
                '=' => self.single_character_token(TokenKind::Equal),
                ';' => self.single_character_token(TokenKind::Semicolon),
                '.' => self.single_character_token(TokenKind::Dot),
                ',' => self.single_character_token(TokenKind::Comma),
                '(' => self.single_character_token(TokenKind::LeftParen),
                ')' => self.single_character_token(TokenKind::RightParen),
                '+' => self.single_character_token(TokenKind::Plus),
                '-' => self.single_character_token(TokenKind::Minus),
                '*' => self.single_character_token(TokenKind::Star),
                '/' => self.single_character_token(TokenKind::Slash),
                _ => return Err(self.invalid_character(start, character)),
            };

            tokens.push(Token {
                kind,
                span: Span {
                    start,
                    end: self.position,
                },
            });
        }

        Ok(tokens)
    }

    fn lex_identifier_or_keyword(&mut self) -> TokenKind {
        let mut identifier = String::new();

        while self.peek().is_some_and(is_identifier_continue) {
            identifier.push(self.next().expect("peeked character should exist"));
        }

        match identifier.to_ascii_uppercase().as_str() {
            "PROGRAM" => TokenKind::Program,
            "END_PROGRAM" => TokenKind::EndProgram,
            "VAR" => TokenKind::Var,
            "END_VAR" => TokenKind::EndVar,
            "VAR_INPUT" => TokenKind::VarInput,
            "VAR_OUTPUT" => TokenKind::VarOutput,
            "BOOL" => TokenKind::Bool,
            "INT" => TokenKind::Int,
            "DINT" => TokenKind::Dint,
            "REAL" => TokenKind::Real,
            "TRUE" => TokenKind::True,
            "FALSE" => TokenKind::False,
            "IF" => TokenKind::If,
            "THEN" => TokenKind::Then,
            "ELSE" => TokenKind::Else,
            "END_IF" => TokenKind::EndIf,
            "AND" => TokenKind::And,
            "OR" => TokenKind::Or,
            "NOT" => TokenKind::Not,
            "R_TRIG" => TokenKind::RTrig,
            "F_TRIG" => TokenKind::FTrig,
            "TON" => TokenKind::Ton,
            "TOF" => TokenKind::Tof,
            _ => TokenKind::Identifier(identifier),
        }
    }

    fn lex_number(&mut self, start: Position) -> Result<TokenKind, LexError> {
        let mut literal = String::new();
        let mut is_real = false;

        while self
            .peek()
            .is_some_and(|character| character.is_ascii_digit())
        {
            literal.push(self.next().expect("peeked character should exist"));
        }

        if self.peek() == Some('.') {
            is_real = true;
            literal.push(self.next().expect("peeked decimal point should exist"));
            while self
                .peek()
                .is_some_and(|character| character.is_ascii_digit())
            {
                literal.push(self.next().expect("peeked character should exist"));
            }
        }

        if matches!(self.peek(), Some('E' | 'e')) {
            is_real = true;
            literal.push(self.next().expect("peeked exponent marker should exist"));
            if matches!(self.peek(), Some('+' | '-')) {
                literal.push(self.next().expect("peeked exponent sign should exist"));
            }

            let exponent_start = literal.len();
            while self
                .peek()
                .is_some_and(|character| character.is_ascii_digit())
            {
                literal.push(self.next().expect("peeked character should exist"));
            }
            if literal.len() == exponent_start {
                return Err(LexError {
                    span: Span {
                        start,
                        end: self.position,
                    },
                    message: format!("invalid REAL literal '{literal}'"),
                });
            }
        }

        Ok(if is_real {
            TokenKind::RealLiteral(literal)
        } else {
            TokenKind::Integer(literal)
        })
    }

    fn skip_block_comment(&mut self, start: Position) -> Result<(), LexError> {
        self.next();
        self.next();

        while let Some(character) = self.next() {
            if character == '*' && self.peek() == Some(')') {
                self.next();
                return Ok(());
            }
        }

        Err(LexError {
            span: Span {
                start,
                end: self.position,
            },
            message: "unterminated block comment".to_owned(),
        })
    }

    fn less_than_token(&mut self) -> TokenKind {
        self.next();

        match self.peek() {
            Some('=') => {
                self.next();
                TokenKind::LessOrEqual
            }
            Some('>') => {
                self.next();
                TokenKind::NotEqual
            }
            _ => TokenKind::Less,
        }
    }

    fn one_or_two_character_token(
        &mut self,
        single: TokenKind,
        second_character: char,
        double: TokenKind,
    ) -> TokenKind {
        self.next();

        if self.peek() == Some(second_character) {
            self.next();
            double
        } else {
            single
        }
    }

    fn single_character_token(&mut self, kind: TokenKind) -> TokenKind {
        self.next();
        kind
    }

    fn invalid_character(&self, start: Position, character: char) -> LexError {
        let mut end = start;
        advance_position(&mut end, character);

        LexError {
            span: Span { start, end },
            message: format!("unexpected character {character:?}"),
        }
    }

    fn peek(&mut self) -> Option<char> {
        self.characters.peek().copied()
    }

    fn peek_next_is(&mut self, expected: char) -> bool {
        let mut characters = self.characters.clone();
        characters.next();
        characters.next() == Some(expected)
    }

    fn next(&mut self) -> Option<char> {
        let character = self.characters.next()?;
        advance_position(&mut self.position, character);
        Some(character)
    }
}

fn is_identifier_start(character: char) -> bool {
    character.is_ascii_alphabetic() || character == '_'
}

fn is_identifier_continue(character: char) -> bool {
    is_identifier_start(character) || character.is_ascii_digit()
}

fn advance_position(position: &mut Position, character: char) {
    position.offset += character.len_utf8();

    if character == '\n' {
        position.line += 1;
        position.column = 1;
    } else {
        position.column += 1;
    }
}
