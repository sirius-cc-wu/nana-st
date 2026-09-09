#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Position {
    pub offset: usize,
    pub line: usize,
    pub column: usize,
}

impl Position {
    pub const fn start() -> Self {
        Self {
            offset: 0,
            line: 1,
            column: 1,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Span {
    pub start: Position,
    pub end: Position,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Token {
    pub kind: TokenKind,
    pub span: Span,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum TokenKind {
    Program,
    EndProgram,
    Var,
    EndVar,
    VarInput,
    VarOutput,
    Bool,
    Int,
    Dint,
    Real,
    True,
    False,
    If,
    Then,
    Else,
    EndIf,
    And,
    Or,
    Not,
    RTrig,
    FTrig,
    Ton,
    Tof,
    Identifier(String),
    Integer(String),
    RealLiteral(String),
    Assign,
    Colon,
    Semicolon,
    Dot,
    Comma,
    LeftParen,
    RightParen,
    Plus,
    Minus,
    Star,
    Slash,
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Identifier {
    pub name: String,
    pub span: Span,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum StorageClass {
    Local,
    Input,
    Output,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DataType {
    Bool,
    Int,
    Dint,
    Real,
    RTrig,
    FTrig,
    Ton,
    Tof,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Declaration {
    pub storage: StorageClass,
    pub name: Identifier,
    pub data_type: DataType,
    pub initializer: Option<Expression>,
    pub span: Span,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Program {
    pub name: Identifier,
    pub declarations: Vec<Declaration>,
    pub statements: Vec<Statement>,
    pub span: Span,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct NamedArgument {
    pub name: Identifier,
    pub value: Expression,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Statement {
    pub kind: StatementKind,
    pub span: Span,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum StatementKind {
    Assignment {
        target: Identifier,
        field: Option<Identifier>,
        value: Expression,
    },
    Invocation {
        instance: Identifier,
        arguments: Vec<NamedArgument>,
    },
    If {
        condition: Expression,
        then_body: Vec<Statement>,
        else_body: Vec<Statement>,
    },
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Expression {
    pub kind: ExpressionKind,
    pub span: Span,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ExpressionKind {
    Boolean(bool),
    Integer(String),
    Real(String),
    Variable(Identifier),
    FieldAccess {
        instance: Identifier,
        field: Identifier,
    },

    Unary {
        operator: UnaryOperator,
        expression: Box<Expression>,
    },
    Binary {
        operator: BinaryOperator,
        left: Box<Expression>,
        right: Box<Expression>,
    },
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum UnaryOperator {
    Not,
    Negate,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BinaryOperator {
    Or,
    And,
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
    Add,
    Subtract,
    Multiply,
    Divide,
}
