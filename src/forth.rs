use std::error::Error;
use std::fmt;

use crate::ast::{BinaryOperator, DataType, StorageClass, UnaryOperator};
use crate::sema::{
    AnalyzedExpression, AnalyzedExpressionKind, AnalyzedProgram, AnalyzedStatement,
    AnalyzedStatementKind, AnalyzedTimerUpdate, AnalyzedVariable, ConstantValue,
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
            let declaration = if variable.data_type == DataType::Real {
                "FVARIABLE"
            } else {
                "VARIABLE"
            };
            declarations.push(format!("{declaration} {}", Self::variable_name(variable)));
        }
        if !declarations.is_empty() {
            parts.push(declarations.join(" "));
        }

        let mut init_tokens = vec![":".to_string(), "nana-init".to_string()];
        for variable in &self.program.variables {
            match variable.storage {
                StorageClass::Output => {
                    Self::emit_zero(variable, &mut init_tokens);
                    Self::emit_store(variable, &mut init_tokens);
                }
                StorageClass::Local => {
                    if let Some(initializer) = &variable.initializer {
                        self.emit_expression(initializer, &mut init_tokens)?;
                    } else {
                        Self::emit_zero(variable, &mut init_tokens);
                    }
                    Self::emit_store(variable, &mut init_tokens);
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
                Self::emit_store(target_var, tokens);
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
            AnalyzedStatementKind::TimerUpdate(update) => {
                self.emit_timer_update(update, tokens)?;
            }
        }
        Ok(())
    }

    fn emit_timer_update(
        &self,
        update: &AnalyzedTimerUpdate,
        tokens: &mut Vec<String>,
    ) -> Result<(), ForthError> {
        match update {
            AnalyzedTimerUpdate::RTrig { clk, m_var, q_var } => {
                let m_var = self
                    .program
                    .variables
                    .get(*m_var)
                    .ok_or_else(|| ForthError {
                        message: format!("m_var index {m_var} out of bounds"),
                    })?;
                let q_var = self
                    .program
                    .variables
                    .get(*q_var)
                    .ok_or_else(|| ForthError {
                        message: format!("q_var index {q_var} out of bounds"),
                    })?;
                self.emit_expression(clk, tokens)?;
                tokens.push("IF".to_string());
                tokens.push(Self::variable_name(m_var));
                tokens.extend([
                    "@".to_string(),
                    "0=".to_string(),
                    "IF".to_string(),
                    "1".to_string(),
                    "ELSE".to_string(),
                    "0".to_string(),
                    "THEN".to_string(),
                ]);
                tokens.push(Self::variable_name(q_var));
                tokens.push("!".to_string());
                tokens.push("1".to_string());
                tokens.push(Self::variable_name(m_var));
                tokens.push("!".to_string());
                tokens.push("ELSE".to_string());
                tokens.push("0".to_string());
                tokens.push(Self::variable_name(q_var));
                tokens.push("!".to_string());
                tokens.push("0".to_string());
                tokens.push(Self::variable_name(m_var));
                tokens.push("!".to_string());
                tokens.push("THEN".to_string());
            }
            AnalyzedTimerUpdate::FTrig { clk, m_var, q_var } => {
                let m_var = self
                    .program
                    .variables
                    .get(*m_var)
                    .ok_or_else(|| ForthError {
                        message: format!("m_var index {m_var} out of bounds"),
                    })?;
                let q_var = self
                    .program
                    .variables
                    .get(*q_var)
                    .ok_or_else(|| ForthError {
                        message: format!("q_var index {q_var} out of bounds"),
                    })?;
                self.emit_expression(clk, tokens)?;
                tokens.push("IF".to_string());
                tokens.push("0".to_string());
                tokens.push(Self::variable_name(q_var));
                tokens.push("!".to_string());
                tokens.push("1".to_string());
                tokens.push(Self::variable_name(m_var));
                tokens.push("!".to_string());
                tokens.push("ELSE".to_string());
                tokens.push(Self::variable_name(m_var));
                tokens.extend([
                    "@".to_string(),
                    "1".to_string(),
                    "=".to_string(),
                    "IF".to_string(),
                    "1".to_string(),
                    "ELSE".to_string(),
                    "0".to_string(),
                    "THEN".to_string(),
                ]);
                tokens.push(Self::variable_name(q_var));
                tokens.push("!".to_string());
                tokens.push("0".to_string());
                tokens.push(Self::variable_name(m_var));
                tokens.push("!".to_string());
                tokens.push("THEN".to_string());
            }
            AnalyzedTimerUpdate::Ton {
                in_expr,
                pt_expr,
                et_var,
                pt_var,
                q_var,
                dt_var,
            } => {
                let et_var = self
                    .program
                    .variables
                    .get(*et_var)
                    .ok_or_else(|| ForthError {
                        message: format!("et_var index {et_var} out of bounds"),
                    })?;
                let pt_var = self
                    .program
                    .variables
                    .get(*pt_var)
                    .ok_or_else(|| ForthError {
                        message: format!("pt_var index {pt_var} out of bounds"),
                    })?;
                let q_var = self
                    .program
                    .variables
                    .get(*q_var)
                    .ok_or_else(|| ForthError {
                        message: format!("q_var index {q_var} out of bounds"),
                    })?;

                self.emit_expression(pt_expr, tokens)?;
                tokens.push(Self::variable_name(pt_var));
                tokens.push("!".to_string());

                self.emit_expression(in_expr, tokens)?;
                tokens.push("IF".to_string());

                tokens.push(Self::variable_name(et_var));
                tokens.push("@".to_string());
                self.emit_dt(*dt_var, tokens)?;
                tokens.extend([
                    "+".to_string(),
                    Self::variable_name(et_var),
                    "!".to_string(),
                ]);

                tokens.push(Self::variable_name(et_var));
                tokens.push("@".to_string());
                tokens.push(Self::variable_name(pt_var));
                tokens.extend([
                    "@".to_string(),
                    ">".to_string(),
                    "IF".to_string(),
                    Self::variable_name(pt_var),
                    "@".to_string(),
                    Self::variable_name(et_var),
                    "!".to_string(),
                    "THEN".to_string(),
                ]);

                tokens.push(Self::variable_name(et_var));
                tokens.push("@".to_string());
                tokens.push(Self::variable_name(pt_var));
                tokens.extend([
                    "@".to_string(),
                    "<".to_string(),
                    "0=".to_string(),
                    "IF".to_string(),
                    "1".to_string(),
                    "ELSE".to_string(),
                    "0".to_string(),
                    "THEN".to_string(),
                    Self::variable_name(q_var),
                    "!".to_string(),
                ]);

                tokens.push("ELSE".to_string());
                tokens.extend([
                    "0".to_string(),
                    Self::variable_name(et_var),
                    "!".to_string(),
                    "0".to_string(),
                    Self::variable_name(q_var),
                    "!".to_string(),
                    "THEN".to_string(),
                ]);
            }
            AnalyzedTimerUpdate::Tof {
                in_expr,
                pt_expr,
                et_var,
                pt_var,
                q_var,
                dt_var,
            } => {
                let et_var = self
                    .program
                    .variables
                    .get(*et_var)
                    .ok_or_else(|| ForthError {
                        message: format!("et_var index {et_var} out of bounds"),
                    })?;
                let pt_var = self
                    .program
                    .variables
                    .get(*pt_var)
                    .ok_or_else(|| ForthError {
                        message: format!("pt_var index {pt_var} out of bounds"),
                    })?;
                let q_var = self
                    .program
                    .variables
                    .get(*q_var)
                    .ok_or_else(|| ForthError {
                        message: format!("q_var index {q_var} out of bounds"),
                    })?;

                self.emit_expression(pt_expr, tokens)?;
                tokens.push(Self::variable_name(pt_var));
                tokens.push("!".to_string());

                self.emit_expression(in_expr, tokens)?;
                tokens.push("IF".to_string());

                tokens.extend([
                    "1".to_string(),
                    Self::variable_name(q_var),
                    "!".to_string(),
                    "0".to_string(),
                    Self::variable_name(et_var),
                    "!".to_string(),
                    "ELSE".to_string(),
                ]);

                tokens.push(Self::variable_name(q_var));
                tokens.extend(["@".to_string(), "IF".to_string()]);

                tokens.push(Self::variable_name(et_var));
                tokens.push("@".to_string());
                tokens.push(Self::variable_name(pt_var));
                tokens.extend(["@".to_string(), "<".to_string(), "IF".to_string()]);

                tokens.push(Self::variable_name(et_var));
                tokens.push("@".to_string());
                self.emit_dt(*dt_var, tokens)?;
                tokens.extend([
                    "+".to_string(),
                    Self::variable_name(et_var),
                    "!".to_string(),
                ]);

                tokens.push(Self::variable_name(et_var));
                tokens.push("@".to_string());
                tokens.push(Self::variable_name(pt_var));
                tokens.extend([
                    "@".to_string(),
                    ">".to_string(),
                    "IF".to_string(),
                    Self::variable_name(pt_var),
                    "@".to_string(),
                    Self::variable_name(et_var),
                    "!".to_string(),
                    "THEN".to_string(),
                ]);

                tokens.push("THEN".to_string());

                tokens.push(Self::variable_name(et_var));
                tokens.push("@".to_string());
                tokens.push(Self::variable_name(pt_var));
                tokens.extend([
                    "@".to_string(),
                    "<".to_string(),
                    "0=".to_string(),
                    "IF".to_string(),
                    "0".to_string(),
                    Self::variable_name(q_var),
                    "!".to_string(),
                    "THEN".to_string(),
                ]);

                tokens.push("THEN".to_string());
                tokens.push("THEN".to_string());
            }
        }
        Ok(())
    }

    fn emit_dt(&self, dt_var: Option<usize>, tokens: &mut Vec<String>) -> Result<(), ForthError> {
        if let Some(var_idx) = dt_var {
            let var = self
                .program
                .variables
                .get(var_idx)
                .ok_or_else(|| ForthError {
                    message: format!("cycle_dt variable index {var_idx} out of bounds"),
                })?;
            tokens.push(Self::variable_name(var));
            tokens.push("@".to_string());
        } else {
            tokens.push("1".to_string());
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
            AnalyzedExpressionKind::Real(value) => tokens.push(value.clone()),
            AnalyzedExpressionKind::Variable { variable } => {
                let var = self
                    .program
                    .variables
                    .get(*variable)
                    .ok_or_else(|| ForthError {
                        message: format!("variable index {variable} out of bounds"),
                    })?;
                tokens.push(Self::variable_name(var));
                tokens.push(
                    if var.data_type == DataType::Real {
                        "F@"
                    } else {
                        "@"
                    }
                    .to_string(),
                );
            }
            AnalyzedExpressionKind::Unary {
                operator,
                expression,
                ..
            } => {
                self.emit_expression(expression, tokens)?;
                match operator {
                    UnaryOperator::Not => tokens.push("0=".to_string()),
                    UnaryOperator::Negate => tokens.push(
                        if expression.data_type == DataType::Real {
                            "FNEGATE"
                        } else {
                            "NEGATE"
                        }
                        .to_string(),
                    ),
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
                self.emit_binary_operator(*operator, left.data_type == DataType::Real, tokens);
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

    fn emit_binary_operator(
        &self,
        operator: BinaryOperator,
        float_operands: bool,
        tokens: &mut Vec<String>,
    ) {
        if float_operands {
            let word = match operator {
                BinaryOperator::Add => "F+",
                BinaryOperator::Subtract => "F-",
                BinaryOperator::Multiply => "F*",
                BinaryOperator::Divide => "F/",
                BinaryOperator::Equal => "F=",
                BinaryOperator::NotEqual => "F<>",
                BinaryOperator::Less => "F<",
                BinaryOperator::LessOrEqual => "F<=",
                BinaryOperator::Greater => "F>",
                BinaryOperator::GreaterOrEqual => "F>=",
                BinaryOperator::And | BinaryOperator::Or => unreachable!("float logic is invalid"),
            };
            tokens.push(word.to_string());
            return;
        }

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

    fn emit_zero(variable: &AnalyzedVariable, tokens: &mut Vec<String>) {
        tokens.push(
            if variable.data_type == DataType::Real {
                "0.0"
            } else {
                "0"
            }
            .to_string(),
        );
    }

    fn emit_store(variable: &AnalyzedVariable, tokens: &mut Vec<String>) {
        tokens.push(Self::variable_name(variable));
        tokens.push(
            if variable.data_type == DataType::Real {
                "F!"
            } else {
                "!"
            }
            .to_string(),
        );
    }

    fn variable_name(variable: &AnalyzedVariable) -> String {
        match variable.storage {
            StorageClass::Input => format!("nana-input-{}", variable.io_index.unwrap_or(0)),
            StorageClass::Output => format!("nana-output-{}", variable.io_index.unwrap_or(0)),
            StorageClass::Local => format!("nana-var-{}", variable.name.to_ascii_lowercase()),
        }
    }
}
