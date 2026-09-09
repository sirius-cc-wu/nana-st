PROGRAM DosingControl
VAR_INPUT
    dosing_mode : INT;
    ph_val : REAL;
    ph_high_limit : REAL;
    ph_low_limit : REAL;
    cond_val : REAL;
    cond_high_limit : REAL;
    cond_low_limit : REAL;
END_VAR
VAR_OUTPUT
    hno3_pump : BOOL;
    naoh_pump : BOOL;
    nano3_pump : BOOL;
END_VAR

IF dosing_mode = 0 THEN
    hno3_pump := FALSE;
    naoh_pump := FALSE;
    nano3_pump := FALSE;

    IF ph_val > ph_high_limit THEN
        hno3_pump := TRUE;
    END_IF;

    IF ph_val < ph_low_limit THEN
        naoh_pump := TRUE;
    END_IF;

    IF cond_val < cond_low_limit THEN
        nano3_pump := TRUE;
    END_IF;

    IF cond_val > cond_high_limit THEN
        nano3_pump := FALSE;
    END_IF;
END_IF;
END_PROGRAM
