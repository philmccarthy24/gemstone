parser grammar FanucGCodeParser;

options { tokenVocab=FanucGCodeLexer; }

// Is the program number optional for top level programs?
program: START_END_PROGRAM EOB programNumber block+ START_END_PROGRAM;

programNumber: PROGRAM_NUMBER_PREFIX INTEGER comment? EOB;

sequenceNumber: SEQUENCE_NUMBER_PREFIX INTEGER;

comment: CTRL_OUT CTRL_OUT_TEXT CTRL_IN;

block: sequenceNumber? comment;

