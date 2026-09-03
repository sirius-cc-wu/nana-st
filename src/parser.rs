use std::error::Error;
use std::fmt;

use crate::ast::{
    BinaryOperator, DataType, Declaration, Expression, ExpressionKind, Identifier, Position,
    Program, Span, Statement, StatementKind, StorageClass, Token, TokenKind, UnaryOperator,
};

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ParseError {
    pub span: Span,
    pub message: String,
}

impl fmt::Display for ParseError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            formatter,
            "{} at {}:{}",
            self.message, self.span.start.line, self.span.start.column
        )
    }
}

impl Error for ParseError {}

pub fn parse(tokens: Vec<Token>) -> Result<Program, ParseError> {
    Parser::new(tokens).parse_program()
}

struct Parser {
    tokens: Vec<Token>,
    index: usize,
    end_of_input: Position,
}

impl Parser {
    fn new(tokens: Vec<Token>) -> Self {
        let end_of_input = tokens
            .last()
            .map(|token| token.span.end)
            .unwrap_or_else(Position::start);

        Self {
            tokens,
            index: 0,
            end_of_input,
        }
    }

    fn parse_program(&mut self) -> Result<Program, ParseError> {
        let start = self.expect(TokenKind::Program, "'PROGRAM'")?.span.start;
        let name = self.parse_identifier()?;
        let mut declarations = Vec::new();

        while matches!(
            self.peek_kind(),
            Some(TokenKind::Var | TokenKind::VarInput | TokenKind::VarOutput)
        ) {
            declarations.extend(self.parse_declaration_block()?);
        }

        let statements = self.parse_statements_until(is_end_program)?;
        let end = self
            .expect(TokenKind::EndProgram, "'END_PROGRAM'")?
            .span
            .end;

        if self.peek_kind().is_some() {
            return Err(self.error_here("unexpected token after END_PROGRAM"));
        }

        Ok(Program {
            name,
            declarations,
            statements,
            span: Span { start, end },
        })
    }

    fn parse_declaration_block(&mut self) -> Result<Vec<Declaration>, ParseError> {
        let storage = match self.advance() {
            Some(Token {
                kind: TokenKind::Var,
                ..
            }) => StorageClass::Local,
            Some(Token {
                kind: TokenKind::VarInput,
                ..
            }) => StorageClass::Input,
            Some(Token {
                kind: TokenKind::VarOutput,
                ..
            }) => StorageClass::Output,
            _ => return Err(self.error_here("expected a variable declaration block")),
        };
        let mut declarations = Vec::new();

        while !self.peek_is(&TokenKind::EndVar) {
            if self.peek_kind().is_none() {
                return Err(self.error_here("expected 'END_VAR'"));
            }
            declarations.push(self.parse_declaration(storage)?);
        }

        self.expect(TokenKind::EndVar, "'END_VAR'")?;
        Ok(declarations)
    }

    fn parse_declaration(&mut self, storage: StorageClass) -> Result<Declaration, ParseError> {
        let name = self.parse_identifier()?;
        let start = name.span.start;
        self.expect(TokenKind::Colon, "':'")?;
        let data_type = self.parse_data_type()?;
        let initializer = if self.peek_is(&TokenKind::Assign) {
            self.advance();
            Some(self.parse_expression()?)
        } else {
            None
        };
        let end = self.expect(TokenKind::Semicolon, "';'")?.span.end;

        Ok(Declaration {
            storage,
            name,
            data_type,
            initializer,
            span: Span { start, end },
        })
    }

    fn parse_data_type(&mut self) -> Result<DataType, ParseError> {
        let token = self
            .advance()
            .ok_or_else(|| self.error_here("expected a type"))?;

        match token.kind {
            TokenKind::Bool => Ok(DataType::Bool),
            TokenKind::Int => Ok(DataType::Int),
            TokenKind::Dint => Ok(DataType::Dint),
            _ => Err(ParseError {
                span: token.span,
                message: "expected one of 'BOOL', 'INT', or 'DINT'".to_owned(),
            }),
        }
    }

    fn parse_statements_until(
        &mut self,
        is_terminator: fn(&TokenKind) -> bool,
    ) -> Result<Vec<Statement>, ParseError> {
        let mut statements = Vec::new();

        while let Some(kind) = self.peek_kind() {
            if is_terminator(kind) {
                return Ok(statements);
            }
            statements.push(self.parse_statement()?);
        }

        Err(self.error_here("expected a statement terminator"))
    }

    fn parse_statement(&mut self) -> Result<Statement, ParseError> {
        match self.peek_kind() {
            Some(TokenKind::Identifier(_)) => self.parse_assignment(),
            Some(TokenKind::If) => self.parse_if_statement(),
            _ => Err(self.error_here("expected an assignment or 'IF' statement")),
        }
    }

    fn parse_assignment(&mut self) -> Result<Statement, ParseError> {
        let target = self.parse_identifier()?;
        let start = target.span.start;
        self.expect(TokenKind::Assign, "':='")?;
        let value = self.parse_expression()?;
        let end = self.expect(TokenKind::Semicolon, "';'")?.span.end;

        Ok(Statement {
            kind: StatementKind::Assignment { target, value },
            span: Span { start, end },
        })
    }

    fn parse_if_statement(&mut self) -> Result<Statement, ParseError> {
        let start = self.expect(TokenKind::If, "'IF'")?.span.start;
        let condition = self.parse_expression()?;
        self.expect(TokenKind::Then, "'THEN'")?;
        let then_body = self.parse_statements_until(is_if_clause_end)?;
        let else_body = if self.peek_is(&TokenKind::Else) {
            self.advance();
            self.parse_statements_until(is_end_if)?
        } else {
            Vec::new()
        };
        let mut end = self.expect(TokenKind::EndIf, "'END_IF'")?.span.end;

        if self.peek_is(&TokenKind::Semicolon) {
            end = self.advance().expect("semicolon should exist").span.end;
        }

        Ok(Statement {
            kind: StatementKind::If {
                condition,
                then_body,
                else_body,
            },
            span: Span { start, end },
        })
    }

    fn parse_expression(&mut self) -> Result<Expression, ParseError> {
        self.parse_binary_expression(0)
    }

    fn parse_binary_expression(
        &mut self,
        minimum_precedence: u8,
    ) -> Result<Expression, ParseError> {
        let mut left = self.parse_unary_expression()?;

        while let Some((operator, precedence)) = self.peek_binary_operator() {
            if precedence < minimum_precedence {
                break;
            }

            self.advance();
            let right = self.parse_binary_expression(precedence + 1)?;
            let span = Span {
                start: left.span.start,
                end: right.span.end,
            };
            left = Expression {
                kind: ExpressionKind::Binary {
                    operator,
                    left: Box::new(left),
                    right: Box::new(right),
                },
                span,
            };
        }

        Ok(left)
    }

    fn parse_unary_expression(&mut self) -> Result<Expression, ParseError> {
        let operator = match self.peek_kind() {
            Some(TokenKind::Not) => UnaryOperator::Not,
            Some(TokenKind::Minus) => UnaryOperator::Negate,
            _ => return self.parse_primary_expression(),
        };
        let start = self
            .advance()
            .expect("unary operator should exist")
            .span
            .start;
        let expression = self.parse_unary_expression()?;
        let span = Span {
            start,
            end: expression.span.end,
        };

        Ok(Expression {
            kind: ExpressionKind::Unary {
                operator,
                expression: Box::new(expression),
            },
            span,
        })
    }

    fn parse_primary_expression(&mut self) -> Result<Expression, ParseError> {
        let token = self
            .advance()
            .ok_or_else(|| self.error_here("expected an expression"))?;

        match token.kind {
            TokenKind::True => Ok(Expression {
                kind: ExpressionKind::Boolean(true),
                span: token.span,
            }),
            TokenKind::False => Ok(Expression {
                kind: ExpressionKind::Boolean(false),
                span: token.span,
            }),
            TokenKind::Integer(literal) => Ok(Expression {
                kind: ExpressionKind::Integer(literal),
                span: token.span,
            }),
            TokenKind::Identifier(name) => Ok(Expression {
                kind: ExpressionKind::Variable(Identifier {
                    name,
                    span: token.span,
                }),
                span: token.span,
            }),
            TokenKind::LeftParen => {
                let mut expression = self.parse_expression()?;
                expression.span = Span {
                    start: token.span.start,
                    end: self.expect(TokenKind::RightParen, "')'")?.span.end,
                };
                Ok(expression)
            }
            _ => Err(ParseError {
                span: token.span,
                message: "expected an expression".to_owned(),
            }),
        }
    }

    fn parse_identifier(&mut self) -> Result<Identifier, ParseError> {
        let token = self
            .advance()
            .ok_or_else(|| self.error_here("expected an identifier"))?;

        match token.kind {
            TokenKind::Identifier(name) => Ok(Identifier {
                name,
                span: token.span,
            }),
            _ => Err(ParseError {
                span: token.span,
                message: "expected an identifier".to_owned(),
            }),
        }
    }

    fn peek_binary_operator(&self) -> Option<(BinaryOperator, u8)> {
        let (operator, precedence) = match self.peek_kind()? {
            TokenKind::Or => (BinaryOperator::Or, 1),
            TokenKind::And => (BinaryOperator::And, 2),
            TokenKind::Equal => (BinaryOperator::Equal, 3),
            TokenKind::NotEqual => (BinaryOperator::NotEqual, 3),
            TokenKind::Less => (BinaryOperator::Less, 3),
            TokenKind::LessOrEqual => (BinaryOperator::LessOrEqual, 3),
            TokenKind::Greater => (BinaryOperator::Greater, 3),
            TokenKind::GreaterOrEqual => (BinaryOperator::GreaterOrEqual, 3),
            TokenKind::Plus => (BinaryOperator::Add, 4),
            TokenKind::Minus => (BinaryOperator::Subtract, 4),
            TokenKind::Star => (BinaryOperator::Multiply, 5),
            TokenKind::Slash => (BinaryOperator::Divide, 5),
            _ => return None,
        };

        Some((operator, precedence))
    }

    fn expect(&mut self, expected: TokenKind, description: &str) -> Result<Token, ParseError> {
        let token = self
            .advance()
            .ok_or_else(|| self.error_here(&format!("expected {description}")))?;

        if token.kind == expected {
            Ok(token)
        } else {
            Err(ParseError {
                span: token.span,
                message: format!("expected {description}"),
            })
        }
    }

    fn peek_kind(&self) -> Option<&TokenKind> {
        self.tokens.get(self.index).map(|token| &token.kind)
    }

    fn peek_is(&self, expected: &TokenKind) -> bool {
        self.peek_kind() == Some(expected)
    }

    fn advance(&mut self) -> Option<Token> {
        let token = self.tokens.get(self.index)?.clone();
        self.index += 1;
        Some(token)
    }

    fn error_here(&self, message: &str) -> ParseError {
        let span = self.tokens.get(self.index).map_or(
            Span {
                start: self.end_of_input,
                end: self.end_of_input,
            },
            |token| token.span,
        );

        ParseError {
            span,
            message: message.to_owned(),
        }
    }
}

fn is_end_program(kind: &TokenKind) -> bool {
    matches!(kind, TokenKind::EndProgram)
}

fn is_if_clause_end(kind: &TokenKind) -> bool {
    matches!(kind, TokenKind::Else | TokenKind::EndIf)
}

fn is_end_if(kind: &TokenKind) -> bool {
    matches!(kind, TokenKind::EndIf)
}
