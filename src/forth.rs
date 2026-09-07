use std::error::Error;
use std::fmt;

use crate::ast::{DataType, StorageClass};
use crate::sema::{AnalyzedExpressionKind, AnalyzedProgram, AnalyzedStatementKind};

const PASS_THROUGH_SOURCE: &str = "VARIABLE nana-input-0 VARIABLE nana-output-0 : nana-init 0 nana-output-0 ! ; : nana-scan nana-input-0 @ IF 1 ELSE 0 THEN nana-output-0 ! ;";

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
    let Some((input_index, input)) = find_single_variable(program, StorageClass::Input) else {
        return Err(unsupported_program());
    };
    let Some((output_index, output)) = find_single_variable(program, StorageClass::Output) else {
        return Err(unsupported_program());
    };

    if program.variables.len() != 2
        || input.data_type != DataType::Bool
        || input.io_index != Some(0)
        || output.data_type != DataType::Bool
        || output.io_index != Some(0)
    {
        return Err(unsupported_program());
    }

    let [statement] = program.statements.as_slice() else {
        return Err(unsupported_program());
    };
    let AnalyzedStatementKind::Assignment { target, value } = &statement.kind else {
        return Err(unsupported_program());
    };
    let AnalyzedExpressionKind::Variable { variable } = &value.kind else {
        return Err(unsupported_program());
    };
    if *target != output_index || *variable != input_index {
        return Err(unsupported_program());
    }

    Ok(PASS_THROUGH_SOURCE.to_owned())
}

fn find_single_variable(
    program: &AnalyzedProgram,
    storage: StorageClass,
) -> Option<(usize, &crate::sema::AnalyzedVariable)> {
    let mut variables = program
        .variables
        .iter()
        .enumerate()
        .filter(|(_, variable)| variable.storage == storage);
    let variable = variables.next()?;
    variables.next().is_none().then_some(variable)
}

fn unsupported_program() -> ForthError {
    ForthError {
        message: "the first Forth target supports only one Boolean pass-through input and output"
            .to_owned(),
    }
}
