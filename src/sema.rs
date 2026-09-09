use std::error::Error;
use std::fmt;

use crate::ast::{
    BinaryOperator, DataType, Expression, ExpressionKind, Identifier, NamedArgument, Program, Span,
    Statement, StatementKind, StorageClass, UnaryOperator,
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
    TimerUpdate(AnalyzedTimerUpdate),
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum AnalyzedTimerUpdate {
    RTrig {
        clk: AnalyzedExpression,
        m_var: usize,
        q_var: usize,
    },
    FTrig {
        clk: AnalyzedExpression,
        m_var: usize,
        q_var: usize,
    },
    Ton {
        in_expr: AnalyzedExpression,
        pt_expr: AnalyzedExpression,
        et_var: usize,
        pt_var: usize,
        q_var: usize,
        dt_var: Option<usize>,
    },
    Tof {
        in_expr: AnalyzedExpression,
        pt_expr: AnalyzedExpression,
        et_var: usize,
        pt_var: usize,
        q_var: usize,
        dt_var: Option<usize>,
    },
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AnalyzedInstance {
    pub name: String,
    pub data_type: DataType,
    pub span: Span,
    pub state_vars: InstanceStateVars,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum InstanceStateVars {
    Trigger {
        m_var: usize,
        q_var: usize,
    },
    Timer {
        et_var: usize,
        pt_var: usize,
        q_var: usize,
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
    let CollectionResult {
        variables,
        instances,
        cycle_dt_var,
    } = collect_declarations(&program)?;

    let statements = analyze_statements(&program.statements, &variables, &instances, cycle_dt_var)?;

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

struct CollectionResult {
    variables: Vec<AnalyzedVariable>,
    instances: Vec<AnalyzedInstance>,
    cycle_dt_var: Option<usize>,
}

fn collect_declarations(program: &Program) -> Result<CollectionResult, SemanticError> {
    let mut variables = Vec::with_capacity(program.declarations.len());
    let mut instances = Vec::new();
    let mut input_index = 0;
    let mut output_index = 0;

    for declaration in &program.declarations {
        let name_str = &declaration.name.name;
        if find_variable(&variables, name_str).is_some()
            || find_instance(&instances, name_str).is_some()
        {
            return Err(SemanticError {
                span: declaration.name.span,
                message: format!("duplicate declaration for '{}'", name_str),
            });
        }

        if declaration.data_type.is_primitive_instance() {
            if declaration.storage != StorageClass::Local {
                return Err(SemanticError {
                    span: declaration.span,
                    message: format!(
                        "{} instances are only supported in VAR declarations",
                        declaration.data_type.name()
                    ),
                });
            }
            if declaration.initializer.is_some() {
                return Err(SemanticError {
                    span: declaration.span,
                    message: format!(
                        "{} instances do not support initializers",
                        declaration.data_type.name()
                    ),
                });
            }

            match declaration.data_type {
                DataType::RTrig | DataType::FTrig => {
                    let m_var = variables.len();
                    variables.push(AnalyzedVariable {
                        name: format!("{name_str}-m"),
                        storage: StorageClass::Local,
                        data_type: DataType::Bool,
                        io_index: None,
                        initializer: None,
                        span: declaration.span,
                    });
                    let q_var = variables.len();
                    variables.push(AnalyzedVariable {
                        name: format!("{name_str}-q"),
                        storage: StorageClass::Local,
                        data_type: DataType::Bool,
                        io_index: None,
                        initializer: None,
                        span: declaration.span,
                    });
                    instances.push(AnalyzedInstance {
                        name: name_str.clone(),
                        data_type: declaration.data_type,
                        span: declaration.span,
                        state_vars: InstanceStateVars::Trigger { m_var, q_var },
                    });
                }
                DataType::Ton | DataType::Tof => {
                    let et_var = variables.len();
                    variables.push(AnalyzedVariable {
                        name: format!("{name_str}-et"),
                        storage: StorageClass::Local,
                        data_type: DataType::Dint,
                        io_index: None,
                        initializer: None,
                        span: declaration.span,
                    });
                    let pt_var = variables.len();
                    variables.push(AnalyzedVariable {
                        name: format!("{name_str}-pt"),
                        storage: StorageClass::Local,
                        data_type: DataType::Dint,
                        io_index: None,
                        initializer: None,
                        span: declaration.span,
                    });
                    let q_var = variables.len();
                    variables.push(AnalyzedVariable {
                        name: format!("{name_str}-q"),
                        storage: StorageClass::Local,
                        data_type: DataType::Bool,
                        io_index: None,
                        initializer: None,
                        span: declaration.span,
                    });
                    instances.push(AnalyzedInstance {
                        name: name_str.clone(),
                        data_type: declaration.data_type,
                        span: declaration.span,
                        state_vars: InstanceStateVars::Timer {
                            et_var,
                            pt_var,
                            q_var,
                        },
                    });
                }
                _ => unreachable!(),
            }
        } else {
            if declaration.initializer.is_some() && declaration.storage != StorageClass::Local {
                return Err(SemanticError {
                    span: declaration.span,
                    message: "initializers are only supported for VAR declarations in v0.1"
                        .to_owned(),
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

            let var_index = variables.len();
            variables.push(AnalyzedVariable {
                name: name_str.clone(),
                storage: declaration.storage,
                data_type: declaration.data_type,
                io_index,
                initializer: None,
                span: declaration.span,
            });

            if let Some(initializer_expr) = &declaration.initializer {
                let initializer = analyze_expression(
                    initializer_expr,
                    &variables,
                    &instances,
                    Some(declaration.data_type),
                )?;
                variables[var_index].initializer = Some(initializer);
            }
        }
    }

    let cycle_dt_var = variables
        .iter()
        .position(|v| v.storage == StorageClass::Input && v.name.eq_ignore_ascii_case("cycle_dt"));

    if let Some(idx) = cycle_dt_var
        && !matches!(variables[idx].data_type, DataType::Int | DataType::Dint)
    {
        return Err(SemanticError {
            span: variables[idx].span,
            message: "cycle_dt input must be INT or DINT".to_owned(),
        });
    }

    Ok(CollectionResult {
        variables,
        instances,
        cycle_dt_var,
    })
}

fn find_instance<'a>(
    instances: &'a [AnalyzedInstance],
    name: &str,
) -> Option<&'a AnalyzedInstance> {
    instances
        .iter()
        .find(|inst| inst.name.eq_ignore_ascii_case(name))
}

fn analyze_statements(
    statements: &[Statement],
    variables: &[AnalyzedVariable],
    instances: &[AnalyzedInstance],
    cycle_dt_var: Option<usize>,
) -> Result<Vec<AnalyzedStatement>, SemanticError> {
    statements
        .iter()
        .map(|statement| analyze_statement(statement, variables, instances, cycle_dt_var))
        .collect()
}

fn analyze_statement(
    statement: &Statement,
    variables: &[AnalyzedVariable],
    instances: &[AnalyzedInstance],
    cycle_dt_var: Option<usize>,
) -> Result<AnalyzedStatement, SemanticError> {
    let kind = match &statement.kind {
        StatementKind::Assignment {
            target,
            field,
            value,
        } => {
            if let Some(field) = field {
                if find_instance(instances, &target.name).is_some() {
                    return Err(SemanticError {
                        span: target.span,
                        message: format!(
                            "cannot assign to read-only field '{}' of instance '{}'",
                            field.name, target.name
                        ),
                    });
                } else if find_variable(variables, &target.name).is_some() {
                    return Err(SemanticError {
                        span: target.span,
                        message: format!("cannot access field on variable '{}'", target.name),
                    });
                } else {
                    return Err(SemanticError {
                        span: target.span,
                        message: format!("unknown variable '{}'", target.name),
                    });
                }
            }

            if find_instance(instances, &target.name).is_some() {
                return Err(SemanticError {
                    span: target.span,
                    message: format!("cannot assign to instance '{}'", target.name),
                });
            }

            let target_index = resolve_variable(target, variables)?;
            let target_variable = &variables[target_index];

            if target_variable.storage == StorageClass::Input {
                return Err(SemanticError {
                    span: target.span,
                    message: format!("cannot assign to BNC input '{}'", target.name),
                });
            }

            let value =
                analyze_expression(value, variables, instances, Some(target_variable.data_type))?;
            AnalyzedStatementKind::Assignment {
                target: target_index,
                value,
            }
        }
        StatementKind::Invocation {
            instance,
            arguments,
        } => analyze_invocation(instance, arguments, variables, instances, cycle_dt_var)?,
        StatementKind::If {
            condition,
            then_body,
            else_body,
        } => {
            let condition =
                analyze_expression(condition, variables, instances, Some(DataType::Bool))?;
            let then_body = analyze_statements(then_body, variables, instances, cycle_dt_var)?;
            let else_body = analyze_statements(else_body, variables, instances, cycle_dt_var)?;
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

fn analyze_invocation(
    instance: &Identifier,
    arguments: &[NamedArgument],
    variables: &[AnalyzedVariable],
    instances: &[AnalyzedInstance],
    cycle_dt_var: Option<usize>,
) -> Result<AnalyzedStatementKind, SemanticError> {
    let inst = find_instance(instances, &instance.name).ok_or_else(|| SemanticError {
        span: instance.span,
        message: format!("unknown timer or trigger instance '{}'", instance.name),
    })?;

    match inst.data_type {
        DataType::RTrig | DataType::FTrig => {
            let mut clk_expr = None;
            for arg in arguments {
                if arg.name.name.eq_ignore_ascii_case("CLK") {
                    if clk_expr.is_some() {
                        return Err(SemanticError {
                            span: arg.name.span,
                            message: "duplicate argument 'CLK'".to_owned(),
                        });
                    }
                    let analyzed =
                        analyze_expression(&arg.value, variables, instances, Some(DataType::Bool))?;
                    clk_expr = Some(analyzed);
                } else {
                    return Err(SemanticError {
                        span: arg.name.span,
                        message: format!(
                            "unexpected argument '{}' for '{}', expected 'CLK'",
                            arg.name.name,
                            inst.data_type.name()
                        ),
                    });
                }
            }

            let clk = clk_expr.ok_or_else(|| SemanticError {
                span: instance.span,
                message: format!("missing argument 'CLK' for '{}'", inst.data_type.name()),
            })?;

            let InstanceStateVars::Trigger { m_var, q_var } = inst.state_vars else {
                unreachable!()
            };

            Ok(AnalyzedStatementKind::TimerUpdate(
                if inst.data_type == DataType::RTrig {
                    AnalyzedTimerUpdate::RTrig { clk, m_var, q_var }
                } else {
                    AnalyzedTimerUpdate::FTrig { clk, m_var, q_var }
                },
            ))
        }
        DataType::Ton | DataType::Tof => {
            let mut in_expr = None;
            let mut pt_expr = None;
            for arg in arguments {
                if arg.name.name.eq_ignore_ascii_case("IN") {
                    if in_expr.is_some() {
                        return Err(SemanticError {
                            span: arg.name.span,
                            message: "duplicate argument 'IN'".to_owned(),
                        });
                    }
                    let analyzed =
                        analyze_expression(&arg.value, variables, instances, Some(DataType::Bool))?;
                    in_expr = Some(analyzed);
                } else if arg.name.name.eq_ignore_ascii_case("PT") {
                    if pt_expr.is_some() {
                        return Err(SemanticError {
                            span: arg.name.span,
                            message: "duplicate argument 'PT'".to_owned(),
                        });
                    }
                    let analyzed = analyze_expression(&arg.value, variables, instances, None)?;
                    if !matches!(analyzed.data_type, DataType::Int | DataType::Dint) {
                        return Err(SemanticError {
                            span: analyzed.span,
                            message: format!(
                                "expected INT or DINT for 'PT' but found {}",
                                analyzed.data_type.name()
                            ),
                        });
                    }
                    if is_negative_constant(&analyzed) {
                        return Err(SemanticError {
                            span: analyzed.span,
                            message: "preset duration 'PT' cannot be negative".to_owned(),
                        });
                    }
                    pt_expr = Some(analyzed);
                } else {
                    return Err(SemanticError {
                        span: arg.name.span,
                        message: format!(
                            "unexpected argument '{}' for '{}', expected 'IN' or 'PT'",
                            arg.name.name,
                            inst.data_type.name()
                        ),
                    });
                }
            }

            let in_val = in_expr.ok_or_else(|| SemanticError {
                span: instance.span,
                message: format!("missing argument 'IN' for '{}'", inst.data_type.name()),
            })?;
            let pt_val = pt_expr.ok_or_else(|| SemanticError {
                span: instance.span,
                message: format!("missing argument 'PT' for '{}'", inst.data_type.name()),
            })?;

            let InstanceStateVars::Timer {
                et_var,
                pt_var,
                q_var,
            } = inst.state_vars
            else {
                unreachable!()
            };

            Ok(AnalyzedStatementKind::TimerUpdate(
                if inst.data_type == DataType::Ton {
                    AnalyzedTimerUpdate::Ton {
                        in_expr: in_val,
                        pt_expr: pt_val,
                        et_var,
                        pt_var,
                        q_var,
                        dt_var: cycle_dt_var,
                    }
                } else {
                    AnalyzedTimerUpdate::Tof {
                        in_expr: in_val,
                        pt_expr: pt_val,
                        et_var,
                        pt_var,
                        q_var,
                        dt_var: cycle_dt_var,
                    }
                },
            ))
        }
        _ => unreachable!(),
    }
}

fn is_negative_constant(expression: &AnalyzedExpression) -> bool {
    match expression.constant {
        Some(ConstantValue::Int(value)) => value < 0,
        Some(ConstantValue::Dint(value)) => value < 0,
        _ => false,
    }
}

fn analyze_expression(
    expression: &Expression,
    variables: &[AnalyzedVariable],
    instances: &[AnalyzedInstance],
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
            if find_instance(instances, &identifier.name).is_some() {
                return Err(SemanticError {
                    span: identifier.span,
                    message: format!(
                        "cannot use instance '{}' as a value; access its fields (e.g. '.Q') instead",
                        identifier.name
                    ),
                });
            }
            let variable = resolve_variable(identifier, variables)?;
            let variable_data = &variables[variable];
            AnalyzedExpression {
                kind: AnalyzedExpressionKind::Variable { variable },
                data_type: variable_data.data_type,
                constant: None,
                span: expression.span,
            }
        }
        ExpressionKind::FieldAccess { instance, field } => {
            let inst = match find_instance(instances, &instance.name) {
                Some(inst) => inst,
                None => {
                    if find_variable(variables, &instance.name).is_some() {
                        return Err(SemanticError {
                            span: instance.span,
                            message: format!(
                                "cannot access field on variable '{}'; '{}' is not an instance",
                                instance.name, instance.name
                            ),
                        });
                    } else {
                        return Err(SemanticError {
                            span: instance.span,
                            message: format!("unknown instance '{}'", instance.name),
                        });
                    }
                }
            };

            match inst.data_type {
                DataType::RTrig | DataType::FTrig => {
                    if field.name.eq_ignore_ascii_case("Q") {
                        let InstanceStateVars::Trigger { q_var, .. } = inst.state_vars else {
                            unreachable!()
                        };
                        AnalyzedExpression {
                            kind: AnalyzedExpressionKind::Variable { variable: q_var },
                            data_type: DataType::Bool,
                            constant: None,
                            span: expression.span,
                        }
                    } else {
                        return Err(SemanticError {
                            span: field.span,
                            message: format!(
                                "'{}' has no field '{}', expected 'Q'",
                                inst.data_type.name(),
                                field.name
                            ),
                        });
                    }
                }
                DataType::Ton | DataType::Tof => {
                    if field.name.eq_ignore_ascii_case("Q") {
                        let InstanceStateVars::Timer { q_var, .. } = inst.state_vars else {
                            unreachable!()
                        };
                        AnalyzedExpression {
                            kind: AnalyzedExpressionKind::Variable { variable: q_var },
                            data_type: DataType::Bool,
                            constant: None,
                            span: expression.span,
                        }
                    } else if field.name.eq_ignore_ascii_case("ET") {
                        let InstanceStateVars::Timer { et_var, .. } = inst.state_vars else {
                            unreachable!()
                        };
                        AnalyzedExpression {
                            kind: AnalyzedExpressionKind::Variable { variable: et_var },
                            data_type: DataType::Dint,
                            constant: None,
                            span: expression.span,
                        }
                    } else {
                        return Err(SemanticError {
                            span: field.span,
                            message: format!(
                                "'{}' has no field '{}', expected 'Q' or 'ET'",
                                inst.data_type.name(),
                                field.name
                            ),
                        });
                    }
                }
                _ => unreachable!(),
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
            instances,
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
            instances,
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
    instances: &[AnalyzedInstance],
    expected_type: Option<DataType>,
) -> Result<AnalyzedExpression, SemanticError> {
    match operator {
        UnaryOperator::Not => {
            let operand = analyze_expression(operand, variables, instances, Some(DataType::Bool))?;
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

            let operand = analyze_expression(operand, variables, instances, numeric_expected)?;
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
    instances: &[AnalyzedInstance],
    expected_type: Option<DataType>,
) -> Result<AnalyzedExpression, SemanticError> {
    match operator {
        BinaryOperator::And | BinaryOperator::Or => {
            let left = analyze_expression(left, variables, instances, Some(DataType::Bool))?;
            let right = analyze_expression(right, variables, instances, Some(DataType::Bool))?;
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
            let (left, right) = analyze_equality_operands(left, right, variables, instances)?;
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
            let (left, right) = analyze_numeric_operands(left, right, variables, instances, None)?;
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
            let (left, right) =
                analyze_numeric_operands(left, right, variables, instances, numeric_expected)?;
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
    instances: &[AnalyzedInstance],
) -> Result<(AnalyzedExpression, AnalyzedExpression), SemanticError> {
    if is_numeric_constant_expression(left) && !is_numeric_constant_expression(right) {
        return analyze_numeric_operands(left, right, variables, instances, None);
    }

    let left = analyze_expression(left, variables, instances, None)?;
    match left.data_type {
        DataType::Bool => {
            let right = analyze_expression(right, variables, instances, Some(DataType::Bool))?;
            Ok((left, right))
        }
        DataType::Int | DataType::Dint | DataType::Real => {
            let right = analyze_expression(right, variables, instances, Some(left.data_type))?;
            Ok((left, right))
        }
        _ => Err(SemanticError {
            span: left.span,
            message: format!("cannot compare value of type '{}'", left.data_type.name()),
        }),
    }
}

fn analyze_numeric_operands(
    left: &Expression,
    right: &Expression,
    variables: &[AnalyzedVariable],
    instances: &[AnalyzedInstance],
    expected_type: Option<DataType>,
) -> Result<(AnalyzedExpression, AnalyzedExpression), SemanticError> {
    if let Some(expected_type) = expected_type {
        let left = analyze_expression(left, variables, instances, Some(expected_type))?;
        require_numeric(&left)?;
        let right = analyze_expression(right, variables, instances, Some(expected_type))?;
        return Ok((left, right));
    }

    if is_numeric_constant_expression(left) && !is_numeric_constant_expression(right) {
        let right = analyze_expression(right, variables, instances, None)?;
        require_numeric(&right)?;
        let left = analyze_expression(left, variables, instances, Some(right.data_type))?;
        return Ok((left, right));
    }

    let left = analyze_expression(left, variables, instances, None)?;
    require_numeric(&left)?;
    let right = analyze_expression(right, variables, instances, Some(left.data_type))?;
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
        DataType::Bool
        | DataType::Real
        | DataType::RTrig
        | DataType::FTrig
        | DataType::Ton
        | DataType::Tof => {
            unreachable!("numeric constant requested for non-numeric type")
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
        DataType::Bool
        | DataType::Real
        | DataType::RTrig
        | DataType::FTrig
        | DataType::Ton
        | DataType::Tof => None,
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
    pub fn is_numeric(self) -> bool {
        matches!(self, Self::Int | Self::Dint | Self::Real)
    }

    pub fn is_primitive_instance(self) -> bool {
        matches!(self, Self::RTrig | Self::FTrig | Self::Ton | Self::Tof)
    }

    pub fn name(self) -> &'static str {
        match self {
            Self::Bool => "BOOL",
            Self::Int => "INT",
            Self::Dint => "DINT",
            Self::Real => "REAL",
            Self::RTrig => "R_TRIG",
            Self::FTrig => "F_TRIG",
            Self::Ton => "TON",
            Self::Tof => "TOF",
        }
    }
}
