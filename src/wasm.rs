use std::error::Error;
use std::fmt;

use wasm_encoder::{
    BlockType, CodeSection, ConstExpr, EntityType, ExportKind, ExportSection, Function,
    FunctionSection, GlobalSection, GlobalType, ImportSection, Module, TypeSection, ValType,
};

use crate::ast::{BinaryOperator, DataType, StorageClass, UnaryOperator};
use crate::sema::{
    AnalyzedExpression, AnalyzedExpressionKind, AnalyzedProgram, AnalyzedStatement,
    AnalyzedStatementKind, AnalyzedVariable, ConstantValue,
};

const READ_INPUT_TYPE: u32 = 0;
const WRITE_OUTPUT_TYPE: u32 = 1;
const LIFECYCLE_TYPE: u32 = 2;
const READ_INPUT_FUNCTION: u32 = 0;
const WRITE_OUTPUT_FUNCTION: u32 = 1;
const INIT_FUNCTION: u32 = 2;
const SCAN_FUNCTION: u32 = 3;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct WasmError {
    pub message: String,
}

impl fmt::Display for WasmError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        formatter.write_str(&self.message)
    }
}

impl Error for WasmError {}

pub fn emit(program: &AnalyzedProgram) -> Result<Vec<u8>, WasmError> {
    Emitter { program }.emit_module()
}

struct Emitter<'program> {
    program: &'program AnalyzedProgram,
}

impl<'program> Emitter<'program> {
    fn emit_module(&self) -> Result<Vec<u8>, WasmError> {
        let mut module = Module::new();
        let mut types = TypeSection::new();
        types.ty().function([ValType::I32], [ValType::I32]);
        types.ty().function([ValType::I32, ValType::I32], []);
        types.ty().function([], []);
        module.section(&types);

        let mut imports = ImportSection::new();
        imports.import("bnc", "read_input", EntityType::Function(READ_INPUT_TYPE));
        imports.import(
            "bnc",
            "write_output",
            EntityType::Function(WRITE_OUTPUT_TYPE),
        );
        module.section(&imports);

        let mut functions = FunctionSection::new();
        functions.function(LIFECYCLE_TYPE);
        functions.function(LIFECYCLE_TYPE);
        module.section(&functions);

        let globals = self.emit_globals()?;
        if !globals.is_empty() {
            module.section(&globals);
        }

        let mut exports = ExportSection::new();
        exports.export("nana_init", ExportKind::Func, INIT_FUNCTION);
        exports.export("nana_scan", ExportKind::Func, SCAN_FUNCTION);
        module.section(&exports);

        let mut code = CodeSection::new();
        code.function(&self.emit_init()?);
        code.function(&self.emit_scan()?);
        module.section(&code);

        Ok(module.finish())
    }

    fn emit_globals(&self) -> Result<GlobalSection, WasmError> {
        let mut globals = GlobalSection::new();

        for _ in &self.program.variables {
            globals.global(
                GlobalType {
                    val_type: ValType::I32,
                    mutable: true,
                    shared: false,
                },
                &ConstExpr::i32_const(0),
            );
        }

        Ok(globals)
    }

    fn emit_init(&self) -> Result<Function, WasmError> {
        let mut function = Function::new(Vec::<(u32, ValType)>::new());

        for (variable_index, variable) in self.program.variables.iter().enumerate() {
            if let Some(initializer) = &variable.initializer {
                self.emit_expression(&mut function, initializer)?;
            } else {
                function.instructions().i32_const(0);
            }
            self.emit_normalize(&mut function, variable.data_type);
            function
                .instructions()
                .global_set(to_u32(variable_index, "global index")?);
        }

        function.instructions().end();
        Ok(function)
    }

    fn emit_scan(&self) -> Result<Function, WasmError> {
        let mut function = Function::new(Vec::<(u32, ValType)>::new());

        for (variable_index, variable) in self.program.variables.iter().enumerate() {
            if variable.storage != StorageClass::Input {
                continue;
            }

            function
                .instructions()
                .i32_const(to_i32(variable.io_index, "input index")?)
                .call(READ_INPUT_FUNCTION);
            self.emit_normalize(&mut function, variable.data_type);
            function
                .instructions()
                .global_set(to_u32(variable_index, "global index")?);
        }

        for statement in &self.program.statements {
            self.emit_statement(&mut function, statement)?;
        }

        for (variable_index, variable) in self.program.variables.iter().enumerate() {
            if variable.storage != StorageClass::Output {
                continue;
            }

            function
                .instructions()
                .i32_const(to_i32(variable.io_index, "output index")?)
                .global_get(to_u32(variable_index, "global index")?);
            self.emit_normalize(&mut function, variable.data_type);
            function.instructions().call(WRITE_OUTPUT_FUNCTION);
        }

        function.instructions().end();
        Ok(function)
    }

    fn emit_statement(
        &self,
        function: &mut Function,
        statement: &AnalyzedStatement,
    ) -> Result<(), WasmError> {
        match &statement.kind {
            AnalyzedStatementKind::Assignment { target, value } => {
                let variable = self.variable(*target)?;
                self.emit_expression(function, value)?;
                self.emit_normalize(function, variable.data_type);
                function
                    .instructions()
                    .global_set(to_u32(*target, "global index")?);
            }
            AnalyzedStatementKind::If {
                condition,
                then_body,
                else_body,
            } => {
                self.emit_expression(function, condition)?;
                function.instructions().if_(BlockType::Empty);
                for statement in then_body {
                    self.emit_statement(function, statement)?;
                }
                if !else_body.is_empty() {
                    function.instructions().else_();
                    for statement in else_body {
                        self.emit_statement(function, statement)?;
                    }
                }
                function.instructions().end();
            }
        }

        Ok(())
    }

    fn emit_expression(
        &self,
        function: &mut Function,
        expression: &AnalyzedExpression,
    ) -> Result<(), WasmError> {
        if let Some(constant) = expression.constant {
            self.emit_constant(function, constant);
            return Ok(());
        }

        match &expression.kind {
            AnalyzedExpressionKind::Boolean(value) => {
                function.instructions().i32_const(i32::from(*value));
            }
            AnalyzedExpressionKind::Integer(value) => {
                function.instructions().i32_const(*value);
            }
            AnalyzedExpressionKind::Variable { variable } => {
                function
                    .instructions()
                    .global_get(to_u32(*variable, "global index")?);
            }
            AnalyzedExpressionKind::Unary {
                operator,
                expression,
                ..
            } => match operator {
                UnaryOperator::Not => {
                    self.emit_expression(function, expression)?;
                    function.instructions().i32_eqz();
                }
                UnaryOperator::Negate => {
                    function.instructions().i32_const(0);
                    self.emit_expression(function, expression)?;
                    function.instructions().i32_sub();
                }
            },
            AnalyzedExpressionKind::Binary {
                operator,
                left,
                right,
                ..
            } => {
                self.emit_expression(function, left)?;
                self.emit_expression(function, right)?;
                self.emit_binary_operator(function, *operator);
            }
        }

        self.emit_normalize(function, expression.data_type);
        Ok(())
    }

    fn emit_constant(&self, function: &mut Function, constant: ConstantValue) {
        match constant {
            ConstantValue::Bool(value) => function.instructions().i32_const(i32::from(value)),
            ConstantValue::Int(value) => function.instructions().i32_const(i32::from(value)),
            ConstantValue::Dint(value) => function.instructions().i32_const(value),
        };
    }

    fn emit_binary_operator(&self, function: &mut Function, operator: BinaryOperator) {
        match operator {
            BinaryOperator::Or => {
                function.instructions().i32_or();
            }
            BinaryOperator::And => {
                function.instructions().i32_and();
            }
            BinaryOperator::Equal => {
                function.instructions().i32_eq();
            }
            BinaryOperator::NotEqual => {
                function.instructions().i32_ne();
            }
            BinaryOperator::Less => {
                function.instructions().i32_lt_s();
            }
            BinaryOperator::LessOrEqual => {
                function.instructions().i32_le_s();
            }
            BinaryOperator::Greater => {
                function.instructions().i32_gt_s();
            }
            BinaryOperator::GreaterOrEqual => {
                function.instructions().i32_ge_s();
            }
            BinaryOperator::Add => {
                function.instructions().i32_add();
            }
            BinaryOperator::Subtract => {
                function.instructions().i32_sub();
            }
            BinaryOperator::Multiply => {
                function.instructions().i32_mul();
            }
            BinaryOperator::Divide => {
                function.instructions().i32_div_s();
            }
        }
    }

    fn emit_normalize(&self, function: &mut Function, data_type: DataType) {
        match data_type {
            DataType::Bool => {
                function.instructions().i32_const(0).i32_ne();
            }
            DataType::Int => {
                function
                    .instructions()
                    .i32_const(16)
                    .i32_shl()
                    .i32_const(16)
                    .i32_shr_s();
            }
            DataType::Dint => {}
        }
    }

    fn variable(&self, index: usize) -> Result<&AnalyzedVariable, WasmError> {
        self.program.variables.get(index).ok_or_else(|| WasmError {
            message: format!("semantic variable index {index} is out of bounds"),
        })
    }
}

fn to_u32(value: usize, name: &str) -> Result<u32, WasmError> {
    u32::try_from(value).map_err(|_| WasmError {
        message: format!("{name} {value} exceeds the Wasm index range"),
    })
}

fn to_i32(value: Option<usize>, name: &str) -> Result<i32, WasmError> {
    let value = value.ok_or_else(|| WasmError {
        message: format!("missing {name} for BNC variable"),
    })?;
    i32::try_from(value).map_err(|_| WasmError {
        message: format!("{name} {value} exceeds the BNC ABI range"),
    })
}
