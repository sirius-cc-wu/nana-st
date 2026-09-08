use std::error::Error;
use std::fmt;

use crate::ast::{
    BinaryOperator, DataType, Expression, ExpressionKind, Identifier, Program, Span, Statement,
    StatementKind, StorageClass, UnaryOperator,
};

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SemanticError {
    pub span: Span,
    pub message: String,
}

impl fmt::Display for SemanticError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            formatter,
            "{} at {}:{}",
            self.message, self.span.start.line, self.span.start.column
        )
    }
}

impl Error for SemanticError {}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AnalyzedProgram {
    pub name: String,
    pub variables: Vec<AnalyzedVariable>,
    pub statements: Vec<AnalyzedStatement>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AnalyzedVariable {
    pub name: String,
    pub storage: StorageClass,
    pub data_type: DataType,
    pub io_index: Option<usize>,
    pub initializer: Option<AnalyzedExpression>,
    pub span: Span,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AnalyzedStatement {
    pub kind: AnalyzedStatementKind,
    pub span: Span,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum AnalyzedStatementKind {
    Assignment {
        target: usize,
        value: AnalyzedExpression,
    },
    If {
        condition: AnalyzedExpression,
        then_body: Vec<AnalyzedStatement>,
        else_body: Vec<AnalyzedStatement>,
    },
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AnalyzedExpression {
    pub kind: AnalyzedExpressionKind,
    pub data_type: DataType,
    pub constant: Option<ConstantValue>,
    pub span: Span,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum AnalyzedExpressionKind {
    Boolean(bool),
    Integer(i32),
    Real(String),
    Variable {
        variable: usize,
    },
    Unary {
        operator: UnaryOperator,
        expression: Box<AnalyzedExpression>,
        integer_semantics: Option<IntegerSemantics>,
    },
    Binary {
        operator: BinaryOperator,
        left: Box<AnalyzedExpression>,
        right: Box<AnalyzedExpression>,
        integer_semantics: Option<IntegerSemantics>,
    },
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ConstantValue {
    Bool(bool),
    Int(i16),
    Dint(i32),
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum IntegerSemantics {
    IntWrapping,
    DintWrapping,
}

pub fn analyze(program: Program) -> Result<AnalyzedProgram, SemanticError> {
    let mut variables = collect_variables(&program)?;

    for (index, declaration) in program.declarations.iter().enumerate() {
        if declaration.initializer.is_some() && declaration.storage != StorageClass::Local {
            return Err(SemanticError {
                span: declaration.span,
                message: "initializers are only supported for VAR declarations in v0.1".to_owned(),
            });
        }
        if declaration
            .initializer
            .as_ref()
            .is_some_and(|expression| !is_literal_initializer(expression))
        {
            return Err(SemanticError {
                span: declaration.span,
                message: "a VAR initializer must be a literal in v0.1".to_owned(),
            });
        }

        let initializer = declaration
            .initializer
            .as_ref()
            .map(|expression| {
                analyze_expression(expression, &variables, Some(variables[index].data_type))
            })
            .transpose()?;
        variables[index].initializer = initializer;
    }

    let statements = analyze_statements(&program.statements, &variables)?;

    Ok(AnalyzedProgram {
        name: program.name.name,
        variables,
        statements,
    })
}

fn is_literal_initializer(expression: &Expression) -> bool {
    matches!(
        expression.kind,
        ExpressionKind::Boolean(_) | ExpressionKind::Integer(_) | ExpressionKind::Real(_)
    ) || matches!(
        expression.kind,
        ExpressionKind::Unary {
            operator: UnaryOperator::Negate,
            expression: ref operand,
        } if matches!(operand.kind, ExpressionKind::Integer(_) | ExpressionKind::Real(_))
    )
}

fn collect_variables(program: &Program) -> Result<Vec<AnalyzedVariable>, SemanticError> {
    let mut variables = Vec::with_capacity(program.declarations.len());
    let mut input_index = 0;
    let mut output_index = 0;

    for declaration in &program.declarations {
        if find_variable(&variables, &declaration.name.name).is_some() {
            return Err(SemanticError {
                span: declaration.name.span,
                message: format!("duplicate declaration for '{}'", declaration.name.name),
            });
        }

        let io_index = match declaration.storage {
            StorageClass::Local => None,
            StorageClass::Input => {
                let index = input_index;
                input_index += 1;
                Some(index)
            }
            StorageClass::Output => {
                let index = output_index;
                output_index += 1;
                Some(index)
            }
        };

        variables.push(AnalyzedVariable {
            name: declaration.name.name.clone(),
            storage: declaration.storage,
            data_type: declaration.data_type,
            io_index,
            initializer: None,
            span: declaration.span,
        });
    }

    Ok(variables)
}

fn analyze_statements(
    statements: &[Statement],
    variables: &[AnalyzedVariable],
) -> Result<Vec<AnalyzedStatement>, SemanticError> {
    statements
        .iter()
        .map(|statement| analyze_statement(statement, variables))
        .collect()
}

fn analyze_statement(
    statement: &Statement,
    variables: &[AnalyzedVariable],
) -> Result<AnalyzedStatement, SemanticError> {
    let kind = match &statement.kind {
        StatementKind::Assignment { target, value } => {
            let target_index = resolve_variable(target, variables)?;
            let target_variable = &variables[target_index];

            if target_variable.storage == StorageClass::Input {
                return Err(SemanticError {
                    span: target.span,
                    message: format!("cannot assign to BNC input '{}'", target.name),
                });
            }

            let value = analyze_expression(value, variables, Some(target_variable.data_type))?;
            AnalyzedStatementKind::Assignment {
                target: target_index,
                value,
            }
        }
        StatementKind::If {
            condition,
            then_body,
            else_body,
        } => {
            let condition = analyze_expression(condition, variables, Some(DataType::Bool))?;
            let then_body = analyze_statements(then_body, variables)?;
            let else_body = analyze_statements(else_body, variables)?;
            AnalyzedStatementKind::If {
                condition,
                then_body,
                else_body,
            }
        }
    };

    Ok(AnalyzedStatement {
        kind,
        span: statement.span,
    })
}

fn analyze_expression(
    expression: &Expression,
    variables: &[AnalyzedVariable],
    expected_type: Option<DataType>,
) -> Result<AnalyzedExpression, SemanticError> {
    let analyzed = match &expression.kind {
        ExpressionKind::Boolean(value) => AnalyzedExpression {
            kind: AnalyzedExpressionKind::Boolean(*value),
            data_type: DataType::Bool,
            constant: Some(ConstantValue::Bool(*value)),
            span: expression.span,
        },
        ExpressionKind::Integer(literal) => {
            analyze_integer_literal(literal, expression.span, expected_type, false)?
        }
        ExpressionKind::Real(literal) => analyze_real_literal(literal, expression.span)?,
        ExpressionKind::Variable(identifier) => {
            let variable = resolve_variable(identifier, variables)?;
            let variable_data = &variables[variable];
            AnalyzedExpression {
                kind: AnalyzedExpressionKind::Variable { variable },
                data_type: variable_data.data_type,
                constant: None,
                span: expression.span,
            }
        }
        ExpressionKind::Unary {
            operator,
            expression: operand,
        } => analyze_unary(
            *operator,
            operand,
            expression.span,
            variables,
            expected_type,
        )?,
        ExpressionKind::Binary {
            operator,
            left,
            right,
        } => analyze_binary(
            *operator,
            left,
            right,
            expression.span,
            variables,
            expected_type,
        )?,
    };

    if let Some(expected_type) = expected_type {
        require_type(&analyzed, expected_type)?;
    }

    Ok(analyzed)
}

fn analyze_unary(
    operator: UnaryOperator,
    operand: &Expression,
    span: Span,
    variables: &[AnalyzedVariable],
    expected_type: Option<DataType>,
) -> Result<AnalyzedExpression, SemanticError> {
    match operator {
        UnaryOperator::Not => {
            let operand = analyze_expression(operand, variables, Some(DataType::Bool))?;
            let constant = match operand.constant {
                Some(ConstantValue::Bool(value)) => Some(ConstantValue::Bool(!value)),
                _ => None,
            };

            Ok(AnalyzedExpression {
                kind: AnalyzedExpressionKind::Unary {
                    operator,
                    expression: Box::new(operand),
                    integer_semantics: None,
                },
                data_type: DataType::Bool,
                constant,
                span,
            })
        }
        UnaryOperator::Negate => {
            let numeric_expected = expected_type.filter(|data_type| data_type.is_numeric());
            if let ExpressionKind::Integer(literal) = &operand.kind {
                return analyze_integer_literal(literal, span, numeric_expected, true);
            }

            let operand = analyze_expression(operand, variables, numeric_expected)?;
            require_numeric(&operand)?;
            let data_type = operand.data_type;
            let constant = if data_type == DataType::Real {
                None
            } else {
                operand
                    .constant
                    .as_ref()
                    .map(|value| numeric_constant(-numeric_value(value), data_type, span))
                    .transpose()?
            };

            Ok(AnalyzedExpression {
                kind: AnalyzedExpressionKind::Unary {
                    operator,
                    expression: Box::new(operand),
                    integer_semantics: integer_semantics(data_type),
                },
                data_type,
                constant,
                span,
            })
        }
    }
}

fn analyze_binary(
    operator: BinaryOperator,
    left: &Expression,
    right: &Expression,
    span: Span,
    variables: &[AnalyzedVariable],
    expected_type: Option<DataType>,
) -> Result<AnalyzedExpression, SemanticError> {
    match operator {
        BinaryOperator::And | BinaryOperator::Or => {
            let left = analyze_expression(left, variables, Some(DataType::Bool))?;
            let right = analyze_expression(right, variables, Some(DataType::Bool))?;
            let constant = match (left.constant, right.constant) {
                (Some(ConstantValue::Bool(left)), Some(ConstantValue::Bool(right))) => {
                    Some(ConstantValue::Bool(match operator {
                        BinaryOperator::And => left && right,
                        BinaryOperator::Or => left || right,
                        _ => unreachable!("operator was matched above"),
                    }))
                }
                _ => None,
            };

            Ok(binary_expression(
                operator,
                left,
                right,
                DataType::Bool,
                constant,
                None,
                span,
            ))
        }
        BinaryOperator::Equal | BinaryOperator::NotEqual => {
            let (left, right) = analyze_equality_operands(left, right, variables)?;
            let constant = match (left.constant.as_ref(), right.constant.as_ref()) {
                (Some(left), Some(right)) => {
                    Some(ConstantValue::Bool(compare_equal(operator, left, right)))
                }
                _ => None,
            };

            Ok(binary_expression(
                operator,
                left,
                right,
                DataType::Bool,
                constant,
                None,
                span,
            ))
        }
        BinaryOperator::Less
        | BinaryOperator::LessOrEqual
        | BinaryOperator::Greater
        | BinaryOperator::GreaterOrEqual => {
            let (left, right) = analyze_numeric_operands(left, right, variables, None)?;
            let constant = match (left.constant.as_ref(), right.constant.as_ref()) {
                (Some(left), Some(right)) => Some(ConstantValue::Bool(compare(
                    operator,
                    numeric_value(left),
                    numeric_value(right),
                ))),
                _ => None,
            };

            Ok(binary_expression(
                operator,
                left,
                right,
                DataType::Bool,
                constant,
                None,
                span,
            ))
        }
        BinaryOperator::Add
        | BinaryOperator::Subtract
        | BinaryOperator::Multiply
        | BinaryOperator::Divide => {
            let numeric_expected = expected_type.filter(|data_type| data_type.is_numeric());
            let (left, right) = analyze_numeric_operands(left, right, variables, numeric_expected)?;
            let data_type = left.data_type;
            let constant = match (left.constant.as_ref(), right.constant.as_ref()) {
                (Some(left), Some(right)) => Some(fold_numeric_binary(
                    operator,
                    numeric_value(left),
                    numeric_value(right),
                    data_type,
                    span,
                )?),
                _ => None,
            };

            Ok(binary_expression(
                operator,
                left,
                right,
                data_type,
                constant,
                integer_semantics(data_type),
                span,
            ))
        }
    }
}

fn analyze_equality_operands(
    left: &Expression,
    right: &Expression,
    variables: &[AnalyzedVariable],
) -> Result<(AnalyzedExpression, AnalyzedExpression), SemanticError> {
    if is_numeric_constant_expression(left) && !is_numeric_constant_expression(right) {
        return analyze_numeric_operands(left, right, variables, None);
    }

    let left = analyze_expression(left, variables, None)?;
    match left.data_type {
        DataType::Bool => {
            let right = analyze_expression(right, variables, Some(DataType::Bool))?;
            Ok((left, right))
        }
        DataType::Int | DataType::Dint | DataType::Real => {
            let right = analyze_expression(right, variables, Some(left.data_type))?;
            Ok((left, right))
        }
    }
}

fn analyze_numeric_operands(
    left: &Expression,
    right: &Expression,
    variables: &[AnalyzedVariable],
    expected_type: Option<DataType>,
) -> Result<(AnalyzedExpression, AnalyzedExpression), SemanticError> {
    if let Some(expected_type) = expected_type {
        let left = analyze_expression(left, variables, Some(expected_type))?;
        require_numeric(&left)?;
        let right = analyze_expression(right, variables, Some(expected_type))?;
        return Ok((left, right));
    }

    if is_numeric_constant_expression(left) && !is_numeric_constant_expression(right) {
        let right = analyze_expression(right, variables, None)?;
        require_numeric(&right)?;
        let left = analyze_expression(left, variables, Some(right.data_type))?;
        return Ok((left, right));
    }

    let left = analyze_expression(left, variables, None)?;
    require_numeric(&left)?;
    let right = analyze_expression(right, variables, Some(left.data_type))?;
    Ok((left, right))
}

fn is_numeric_constant_expression(expression: &Expression) -> bool {
    match &expression.kind {
        ExpressionKind::Integer(_) => true,
        ExpressionKind::Unary {
            operator: UnaryOperator::Negate,
            expression,
        } => is_numeric_constant_expression(expression),
        ExpressionKind::Binary {
            operator:
                BinaryOperator::Add
                | BinaryOperator::Subtract
                | BinaryOperator::Multiply
                | BinaryOperator::Divide,
            left,
            right,
            ..
        } => is_numeric_constant_expression(left) && is_numeric_constant_expression(right),
        _ => false,
    }
}

fn binary_expression(
    operator: BinaryOperator,
    left: AnalyzedExpression,
    right: AnalyzedExpression,
    data_type: DataType,
    constant: Option<ConstantValue>,
    integer_semantics: Option<IntegerSemantics>,
    span: Span,
) -> AnalyzedExpression {
    AnalyzedExpression {
        kind: AnalyzedExpressionKind::Binary {
            operator,
            left: Box::new(left),
            right: Box::new(right),
            integer_semantics,
        },
        data_type,
        constant,
        span,
    }
}

fn analyze_integer_literal(
    literal: &str,
    span: Span,
    expected_type: Option<DataType>,
    is_negative: bool,
) -> Result<AnalyzedExpression, SemanticError> {
    let data_type = expected_type.unwrap_or(DataType::Dint);
    if !matches!(data_type, DataType::Int | DataType::Dint) {
        return Err(type_mismatch(span, data_type, DataType::Dint));
    }

    let parsed = literal.parse::<i64>().map_err(|_| SemanticError {
        span,
        message: format!("integer literal '{literal}' is too large"),
    })?;
    let value = if is_negative { -parsed } else { parsed };
    let constant = numeric_constant(value, data_type, span)?;

    Ok(AnalyzedExpression {
        kind: AnalyzedExpressionKind::Integer(value as i32),
        data_type,
        constant: Some(constant),
        span,
    })
}

fn analyze_real_literal(literal: &str, span: Span) -> Result<AnalyzedExpression, SemanticError> {
    let value = literal.parse::<f64>().map_err(|_| SemanticError {
        span,
        message: format!("invalid REAL literal '{literal}'"),
    })?;
    if !value.is_finite() {
        return Err(SemanticError {
            span,
            message: format!("REAL literal '{literal}' is outside the binary64 range"),
        });
    }

    Ok(AnalyzedExpression {
        kind: AnalyzedExpressionKind::Real(literal.to_owned()),
        data_type: DataType::Real,
        constant: None,
        span,
    })
}

fn fold_numeric_binary(
    operator: BinaryOperator,
    left: i64,
    right: i64,
    data_type: DataType,
    span: Span,
) -> Result<ConstantValue, SemanticError> {
    let value = match operator {
        BinaryOperator::Add => left + right,
        BinaryOperator::Subtract => left - right,
        BinaryOperator::Multiply => left * right,
        BinaryOperator::Divide => {
            if right == 0 {
                return Err(SemanticError {
                    span,
                    message: "constant expression divides by zero".to_owned(),
                });
            }
            left / right
        }
        _ => unreachable!("operator was limited to numeric operations"),
    };

    numeric_constant(value, data_type, span)
}

fn numeric_constant(
    value: i64,
    data_type: DataType,
    span: Span,
) -> Result<ConstantValue, SemanticError> {
    match data_type {
        DataType::Int => i16::try_from(value)
            .map(ConstantValue::Int)
            .map_err(|_| SemanticError {
                span,
                message: format!("constant result {value} is outside the INT range"),
            }),
        DataType::Dint => {
            i32::try_from(value)
                .map(ConstantValue::Dint)
                .map_err(|_| SemanticError {
                    span,
                    message: format!("constant result {value} is outside the DINT range"),
                })
        }
        DataType::Bool | DataType::Real => {
            unreachable!("numeric constant requested for BOOL or REAL")
        }
    }
}

fn resolve_variable(
    identifier: &Identifier,
    variables: &[AnalyzedVariable],
) -> Result<usize, SemanticError> {
    find_variable(variables, &identifier.name).ok_or_else(|| SemanticError {
        span: identifier.span,
        message: format!("unknown variable '{}'", identifier.name),
    })
}

fn find_variable(variables: &[AnalyzedVariable], name: &str) -> Option<usize> {
    variables
        .iter()
        .position(|variable| variable.name.eq_ignore_ascii_case(name))
}

fn require_type(
    expression: &AnalyzedExpression,
    expected_type: DataType,
) -> Result<(), SemanticError> {
    if expression.data_type == expected_type {
        Ok(())
    } else {
        Err(type_mismatch(
            expression.span,
            expected_type,
            expression.data_type,
        ))
    }
}

fn require_numeric(expression: &AnalyzedExpression) -> Result<(), SemanticError> {
    if expression.data_type.is_numeric() {
        Ok(())
    } else {
        Err(SemanticError {
            span: expression.span,
            message: format!(
                "expected INT, DINT, or REAL but found {}",
                expression.data_type.name()
            ),
        })
    }
}

fn type_mismatch(span: Span, expected: DataType, found: DataType) -> SemanticError {
    SemanticError {
        span,
        message: format!("expected {} but found {}", expected.name(), found.name()),
    }
}

fn integer_semantics(data_type: DataType) -> Option<IntegerSemantics> {
    match data_type {
        DataType::Bool | DataType::Real => None,
        DataType::Int => Some(IntegerSemantics::IntWrapping),
        DataType::Dint => Some(IntegerSemantics::DintWrapping),
    }
}

fn numeric_value(value: &ConstantValue) -> i64 {
    match value {
        ConstantValue::Int(value) => i64::from(*value),
        ConstantValue::Dint(value) => i64::from(*value),
        ConstantValue::Bool(_) => unreachable!("numeric value requested for BOOL"),
    }
}

fn compare_equal(operator: BinaryOperator, left: &ConstantValue, right: &ConstantValue) -> bool {
    let equals = match (left, right) {
        (ConstantValue::Bool(left), ConstantValue::Bool(right)) => left == right,
        _ => numeric_value(left) == numeric_value(right),
    };

    match operator {
        BinaryOperator::Equal => equals,
        BinaryOperator::NotEqual => !equals,
        _ => unreachable!("operator was limited to equality comparisons"),
    }
}

fn compare(operator: BinaryOperator, left: i64, right: i64) -> bool {
    match operator {
        BinaryOperator::Equal => left == right,
        BinaryOperator::NotEqual => left != right,
        BinaryOperator::Less => left < right,
        BinaryOperator::LessOrEqual => left <= right,
        BinaryOperator::Greater => left > right,
        BinaryOperator::GreaterOrEqual => left >= right,
        _ => unreachable!("operator was limited to comparisons"),
    }
}

impl DataType {
    fn is_numeric(self) -> bool {
        matches!(self, Self::Int | Self::Dint | Self::Real)
    }

    fn name(self) -> &'static str {
        match self {
            Self::Bool => "BOOL",
            Self::Int => "INT",
            Self::Dint => "DINT",
            Self::Real => "REAL",
        }
    }
}
