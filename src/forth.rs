use std::error::Error;
use std::fmt;

use crate::ast::{BinaryOperator, DataType, StorageClass, UnaryOperator};
use crate::sema::{
    AnalyzedExpression, AnalyzedExpressionKind, AnalyzedProgram, AnalyzedStatement,
    AnalyzedStatementKind, AnalyzedVariable, ConstantValue,
};

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ForthError {
    pub message: String,
}

impl fmt::Display for ForthError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        formatter.write_str(&self.message)
    }
}

impl Error for ForthError {}

pub fn emit(program: &AnalyzedProgram) -> Result<String, ForthError> {
    let emitter = ForthEmitter { program };
    emitter.emit_program()
}

struct ForthEmitter<'a> {
    program: &'a AnalyzedProgram,
}

impl<'a> ForthEmitter<'a> {
    fn emit_program(&self) -> Result<String, ForthError> {
        let mut parts = Vec::new();

        let mut declarations = Vec::new();
        for variable in &self.program.variables {
            declarations.push(format!("VARIABLE {}", Self::variable_name(variable)));
        }
        if !declarations.is_empty() {
            parts.push(declarations.join(" "));
        }

        let mut init_tokens = vec![":".to_string(), "nana-init".to_string()];
        for variable in &self.program.variables {
            match variable.storage {
                StorageClass::Output => {
                    init_tokens.push("0".to_string());
                    init_tokens.push(Self::variable_name(variable));
                    init_tokens.push("!".to_string());
                }
                StorageClass::Local => {
                    if let Some(initializer) = &variable.initializer {
                        self.emit_expression(initializer, &mut init_tokens)?;
                    } else {
                        init_tokens.push("0".to_string());
                    }
                    init_tokens.push(Self::variable_name(variable));
                    init_tokens.push("!".to_string());
                }
                StorageClass::Input => {}
            }
        }
        init_tokens.push(";".to_string());
        parts.push(init_tokens.join(" "));

        let mut scan_tokens = vec![":".to_string(), "nana-scan".to_string()];
        for statement in &self.program.statements {
            self.emit_statement(statement, &mut scan_tokens)?;
        }
        scan_tokens.push(";".to_string());
        parts.push(scan_tokens.join(" "));

        Ok(parts.join(" "))
    }

    fn emit_statement(
        &self,
        statement: &AnalyzedStatement,
        tokens: &mut Vec<String>,
    ) -> Result<(), ForthError> {
        match &statement.kind {
            AnalyzedStatementKind::Assignment { target, value } => {
                let target_var = self
                    .program
                    .variables
                    .get(*target)
                    .ok_or_else(|| ForthError {
                        message: format!("target variable index {target} out of bounds"),
                    })?;
                self.emit_expression(value, tokens)?;
                if target_var.data_type == DataType::Bool {
                    tokens.extend([
                        "IF".to_string(),
                        "1".to_string(),
                        "ELSE".to_string(),
                        "0".to_string(),
                        "THEN".to_string(),
                    ]);
                }
                tokens.push(Self::variable_name(target_var));
                tokens.push("!".to_string());
            }
            AnalyzedStatementKind::If {
                condition,
                then_body,
                else_body,
            } => {
                self.emit_expression(condition, tokens)?;
                tokens.push("IF".to_string());
                for stmt in then_body {
                    self.emit_statement(stmt, tokens)?;
                }
                if !else_body.is_empty() {
                    tokens.push("ELSE".to_string());
                    for stmt in else_body {
                        self.emit_statement(stmt, tokens)?;
                    }
                }
                tokens.push("THEN".to_string());
            }
        }
        Ok(())
    }

    fn emit_expression(
        &self,
        expression: &AnalyzedExpression,
        tokens: &mut Vec<String>,
    ) -> Result<(), ForthError> {
        if let Some(constant) = expression.constant {
            self.emit_constant(constant, tokens);
            return Ok(());
        }

        match &expression.kind {
            AnalyzedExpressionKind::Boolean(value) => {
                tokens.push(if *value { "1" } else { "0" }.to_string());
            }
            AnalyzedExpressionKind::Integer(value) => {
                tokens.push(value.to_string());
            }
            AnalyzedExpressionKind::Variable { variable } => {
                let var = self
                    .program
                    .variables
                    .get(*variable)
                    .ok_or_else(|| ForthError {
                        message: format!("variable index {variable} out of bounds"),
                    })?;
                tokens.push(Self::variable_name(var));
                tokens.push("@".to_string());
            }
            AnalyzedExpressionKind::Unary {
                operator,
                expression,
                ..
            } => {
                self.emit_expression(expression, tokens)?;
                match operator {
                    UnaryOperator::Not => tokens.push("0=".to_string()),
                    UnaryOperator::Negate => tokens.push("NEGATE".to_string()),
                }
            }
            AnalyzedExpressionKind::Binary {
                operator,
                left,
                right,
                ..
            } => {
                self.emit_expression(left, tokens)?;
                self.emit_expression(right, tokens)?;
                self.emit_binary_operator(*operator, tokens);
            }
        }
        Ok(())
    }

    fn emit_constant(&self, constant: ConstantValue, tokens: &mut Vec<String>) {
        match constant {
            ConstantValue::Bool(value) => tokens.push(if value { "1" } else { "0" }.to_string()),
            ConstantValue::Int(value) => tokens.push(value.to_string()),
            ConstantValue::Dint(value) => tokens.push(value.to_string()),
        }
    }

    fn emit_binary_operator(&self, operator: BinaryOperator, tokens: &mut Vec<String>) {
        match operator {
            BinaryOperator::Add => tokens.push("+".to_string()),
            BinaryOperator::Subtract => tokens.push("-".to_string()),
            BinaryOperator::Multiply => tokens.push("*".to_string()),
            BinaryOperator::Divide => tokens.push("/".to_string()),
            BinaryOperator::And => tokens.push("AND".to_string()),
            BinaryOperator::Or => tokens.push("OR".to_string()),
            BinaryOperator::Equal => tokens.push("=".to_string()),
            BinaryOperator::NotEqual => tokens.push("<>".to_string()),
            BinaryOperator::Less => tokens.push("<".to_string()),
            BinaryOperator::Greater => tokens.push(">".to_string()),
            BinaryOperator::LessOrEqual => tokens.extend([">".to_string(), "0=".to_string()]),
            BinaryOperator::GreaterOrEqual => tokens.extend(["<".to_string(), "0=".to_string()]),
        }
    }

    fn variable_name(variable: &AnalyzedVariable) -> String {
        match variable.storage {
            StorageClass::Input => format!("nana-input-{}", variable.io_index.unwrap_or(0)),
            StorageClass::Output => format!("nana-output-{}", variable.io_index.unwrap_or(0)),
            StorageClass::Local => format!("nana-var-{}", variable.name.to_ascii_lowercase()),
        }
    }
}
