PROGRAM ChillerControl
VAR_INPUT
    water_temp : REAL;
    high_limit : REAL;
    low_limit : REAL;
END_VAR
VAR_OUTPUT
    chiller_enable : BOOL;
    valve_open : BOOL;
    pump_run : BOOL;
END_VAR

chiller_enable := TRUE;

IF water_temp > high_limit THEN
    valve_open := TRUE;
    pump_run := TRUE;
END_IF;

IF water_temp < low_limit THEN
    valve_open := FALSE;
    pump_run := FALSE;
END_IF;
END_PROGRAM
