parser grammar SiemensGCodeParser;

options { tokenVocab=SiemensGCodeLexer; }

////////////////////////////////////////////////////////////////////////
// Start of Siemens parser definition. Make this quite loose at first,
// then tighten up as more rules become evident.
// Because Siemens is so freeform, it's sensible to do quite a lot of
// checking in the semantic analysis stage



comment: SEMICOLON COMMENT_TEXT EOF;

program: block+;

block: N_WORD? (statement | expr)? comment? BLOCK_END;

statement: gcode_word+
		 | if
		 | goto
		 | label
		 | STOPRE
		 | message
		 ;
// TODO: add lots more statements here

// GCode addresses: Some addresses take parameters in square brackets, eg POS[Axis]=...
// p2-9 and 11 - "Extended addresses" you can have notation like "X4=20" which refers to axis X4, as well as a variable identifier enclosed in square brackets.
addr_extension: integer | (SQUAREDBRACKET_OPEN expr SQUAREDBRACKET_CLOSE);
// Address '=' designation - see p2-13. '=' must appear between addr and param if param is expr.
// if param is a literal (and the addr is one letter), the '=' is optional.
gcode_word: ADDRESS addr_extension? EQUALS expr
		  | ADDRESS (real | integer); // semantic analysis: check ADDRESS is single letter

message: MSG PARENTHESIS_LEFT string PARENTHESIS_RIGHT;

goto: (GOTOB | GOTOF) IDENTIFIER; // !!! are DIGITS to go to an N number allowed also? I think they are.

if: IF PARENTHESIS_LEFT expr PARENTHESIS_RIGHT goto;

label: IDENTIFIER COLON;

/////////// TODO: define variable here, DEF, PROC etc etc

expr: PARENTHESIS_LEFT expr PARENTHESIS_RIGHT
	| (BUILTIN_FUNCTION | IDENTIFIER) PARENTHESIS_LEFT expr (COMMA expr)* PARENTHESIS_RIGHT
	| expr (MULTIPLY|DIVIDE|DIVIDE_2|MODULUS) expr
	| expr (PLUS|MINUS) expr
	| expr EQUALS expr
	| expr RELATIONAL_OP expr										
	| expr LOGICAL_OP expr											
//	| variable														
	| integer														
	| real															
	| bool
	| (PLUS|MINUS) expr												
	;

// type literal rules
string: QUOTE STRING_TEXT QUOTE (CONCAT_OP expr CONCAT_OP)?; // from eg on p2-20. Can you concat more than one var?
real: (PLUS|MINUS)? REAL_VAL;
integer: (PLUS|MINUS)? INTEGER_VAL;
bool: BOOL_VAL;