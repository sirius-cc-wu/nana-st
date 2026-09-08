PROGRAM RealBranch
VAR_INPUT
    input : REAL;
END_VAR
VAR
    offset : REAL := 1.5;
END_VAR
VAR_OUTPUT
    output : REAL;
    high : BOOL;
END_VAR

output := input + offset;
high := output >= 10.0;
END_PROGRAM
