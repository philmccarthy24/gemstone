parser grammar FanucGCodeParser;

options { tokenVocab=FanucGCodeLexer; }

// Is the program number optional for top level programs?
program: START_END_PROGRAM NEWLINE+ programNumber programContent START_END_PROGRAM NEWLINE? EOF;

programContent: block+;

programNumber: PROGRAM_NUMBER_PREFIX DIGITS comment? NEWLINE;

sequenceNumber: SEQUENCE_NUMBER_PREFIX DIGITS;

// in fanuc nomenclature this is a "word" consisting of an address and a number
gcode: GCODE_PREFIX expr;

comment: CTRL_OUT CTRL_OUT_TEXT CTRL_IN;

block: sequenceNumber blockContent? NEWLINE
	 | blockContent? NEWLINE;

// according to p.491, comments can be in front of seq num (or at beginning of block if sn not specified) or at end of block
blockContent: (statement | expr)
			| comment (statement | expr)
			| (statement | expr)? comment
			;

statement: gcode+
		 | if
		 | goto
		 ;

if: IF OPEN_BRACKET expr CLOSE_BRACKET THEN expr
  | IF OPEN_BRACKET expr CLOSE_BRACKET goto
  ;

goto: GOTO DIGITS
	;

//p380 for variable description
variable: HASH DIGITS
		| HASH OPEN_BRACKET expr CLOSE_BRACKET
		| OPEN_BRACKET HASH SYSTEMVAR_CONST_OR_COMMONVAR_IDENTIFIER (OPEN_BRACKET DIGITS CLOSE_BRACKET)? CLOSE_BRACKET
		;

expr: OPEN_BRACKET expr CLOSE_BRACKET								# BracketedExpression
	| expr (PLUS|MINUS) expr										# ArithmeticExpression
	| expr (MULTIPLY|DIVIDE) expr									# ArithmeticExpression
	| expr MOD expr													# ArithmeticExpression
	| expr EQUALS expr												# AssignmentExpression
	| expr RELATIONAL_OP expr										# RelationalExpression
	| expr LOGICAL_OP expr											# LogicalExpression
	| BUILTIN_FUNCTION OPEN_BRACKET expr (COMMA expr)* CLOSE_BRACKET	# FunctionExpression
	| variable														# VariableExpression
	| integer														# IntegerExpression
	| real															# RealExpression
	;
// need to add WHILE DO END, AX, AXNUM, SETVN, BPRNT, DPRNT, POPEN, PCLOS

real: (PLUS|MINUS)? DECIMAL;

integer: (PLUS|MINUS)? DIGITS;