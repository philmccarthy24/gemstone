parser grammar FanucGCodeParser;

options { tokenVocab=FanucGCodeLexer; }

// Is the program number optional for top level programs?
program: START_END_PROGRAM EOB programNumber block+ START_END_PROGRAM;

programNumber: PROGRAM_NUMBER_PREFIX INTEGER comment? EOB;

sequenceNumber: SEQUENCE_NUMBER_PREFIX INTEGER;

// in fanuc nomenclature this is a "word" consisting of an address and a number
gcode: GCODE_PREFIX expr;

comment: CTRL_OUT CTRL_OUT_TEXT CTRL_IN;

// according to p.491, comments can be in front of seq num (or at beginning of block if sn not specified) or at end of block
blockContent: (statement | expr)
			| comment (statement | expr)
			| (statement | expr) comment
			;

block: sequenceNumber blockContent? EOB
	 | blockContent EOB;

statement: gcode+
		 | if
		 | goto
		 ;

if: IF OPEN_BRACKET expr CLOSE_BRACKET THEN expr
  | IF OPEN_BRACKET expr CLOSE_BRACKET goto
  ;

goto: GOTO INTEGER
	;

//p380 for variable description
variable: HASH INTEGER
		| HASH OPEN_BRACKET expr CLOSE_BRACKET
		| OPEN_BRACKET HASH SYSTEMVAR_CONST_OR_COMMONVAR_IDENTIFIER (OPEN_BRACKET INTEGER CLOSE_BRACKET)? CLOSE_BRACKET
		;

real: MINUS? DECIMAL
	;

expr: OPEN_BRACKET expr CLOSE_BRACKET
	| expr PLUS expr
	| expr MINUS expr
	| expr MULTIPLY expr
	| expr DIVIDE expr
	| expr MOD expr
	| expr EQUALS expr
	| expr RELATIONAL_OP expr
	| expr LOGICAL_OP expr
	| BUILTIN_FUNCTION OPEN_BRACKET expr (COMMA expr)* CLOSE_BRACKET
	| variable
	| INTEGER
	| real
	;
// need to add WHILE DO END, AX, AXNUM, SETVN, BPRNT, DPRNT, POPEN, PCLOS