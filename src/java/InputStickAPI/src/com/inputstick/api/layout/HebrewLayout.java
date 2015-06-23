package com.inputstick.api.layout;

public class HebrewLayout extends KeyboardLayout {
	
	public static final String LOCALE_NAME = "he-IL";
	
	public static final int LUT[][] = {
		/*	0	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	1	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,						
		/*	2	*/	{	0	,	(int)'1'	,	0x0021		,	-1		,	-1		,	-1		}	, // TODO SGCap
		/*	3	*/	{	0	,	(int)'2'	,	0x0040		,	-1		,	-1		,	-1		}	, // TODO SGCap
		/*	4	*/	{	0	,	(int)'3'	,	0x0023		,	-1		,	0x200e	,	-1		}	, // TODO SGCap
		/*	5	*/	{	0	,	(int)'4'	,	0x0024		,	-1		,	0x200f	,	0x20aa	}	, // TODO SGCap
		/*	6	*/	{	0	,	(int)'5'	,	0x0025		,	-1		,	-1		,	-1		}	, // TODO SGCap
		/*	7	*/	{	0	,	(int)'6'	,	0x005e		,	-1		,	-1		,	-1		}	, // TODO SGCap
		/*	8	*/	{	0	,	(int)'7'	,	0x0026		,	-1		,	-1		,	-1		}	, // TODO SGCap
		/*	9	*/	{	0	,	(int)'8'	,	0x002a		,	-1		,	-1		,	-1		}	, // TODO SGCap
		/*	0a	*/	{	0	,	(int)'9'	,	0x0029		,	-1		,	-1		,	-1		}	, // TODO SGCap
		/*	0b	*/	{	0	,	(int)'0'	,	0x0028		,	-1		,	-1		,	-1		}	, // TODO SGCap
		/*	0c	*/	{	0	,	0x002d		,	0x005f		,	-1		,	-1		,	0x05bf	}	, // TODO SGCap
		/*	0d	*/	{	0	,	0x003d		,	0x002b		,	-1		,	-1		,	-1		}	, // TODO SGCap
		/*	0e	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	0f	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		
		/*	10	*/	{	1	,	0x002f		,	(int)'Q'	,	-1		,	-1		,	-1		}	,
		/*	11	*/	{	1	,	0x0027		,	(int)'W'	,	-1		,	-1		,	-1		}	,
		/*	12	*/	{	1	,	0x05e7		,	(int)'E'	,	-1		,	-1		,	0x20ac	}	,
		/*	13	*/	{	1	,	0x05e8		,	(int)'R'	,	-1		,	-1		,	-1		}	,
		/*	14	*/	{	1	,	0x05d0		,	(int)'T'	,	-1		,	-1		,	-1		}	,
		/*	15	*/	{	1	,	0x05d8		,	(int)'Y'	,	-1		,	-1		,	-1		}	,
		/*	16	*/	{	1	,	0x05d5		,	(int)'U'	,	-1		,	-1		,	0x05f0	}	,
		/*	17	*/	{	1	,	0x05df		,	(int)'I'	,	-1		,	-1		,	-1		}	,
		/*	18	*/	{	1	,	0x05dd		,	(int)'O'	,	-1		,	-1		,	-1		}	,
		/*	19	*/	{	1	,	0x05e4		,	(int)'P'	,	-1		,	-1		,	-1		}	,
		/*	1a	*/	{	0	,	0x005d		,	0x007d		,	0x200e	,	-1		,	-1		}	, // TODO SGCap
		/*	1b	*/	{	0	,	0x005b		,	0x007b		,	0x200f	,	-1		,	-1		}	, // TODO SGCap
		/*	1c	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	1d	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	1e	*/	{	1	,	0x05e9		,	(int)'A'	,	-1		,	-1		,	-1		}	,
		/*	1f	*/	{	1	,	0x05d3		,	(int)'S'	,	-1		,	-1		,	-1		}	,
		
		/*	20	*/	{	1	,	0x05d2		,	(int)'D'	,	-1		,	-1		,	-1		}	,
		/*	21	*/	{	1	,	0x05db		,	(int)'F'	,	-1		,	-1		,	-1		}	,
		/*	22	*/	{	1	,	0x05e2		,	(int)'G'	,	-1		,	-1		,	-1		}	,
		/*	23	*/	{	1	,	0x05d9		,	(int)'H'	,	-1		,	-1		,	0x05f2	}	,
		/*	24	*/	{	1	,	0x05d7		,	(int)'J'	,	-1		,	-1		,	0x05f1	}	,
		/*	25	*/	{	1	,	0x05dc		,	(int)'K'	,	-1		,	-1		,	-1		}	,
		/*	26	*/	{	1	,	0x05da		,	(int)'L'	,	-1		,	-1		,	-1		}	,
		/*	27	*/	{	0	,	0x05e3		,	0x003a		,	-1		,	-1		,	-1		}	, // TODO SGCap
		/*	28	*/	{	0	,	0x002c		,	0x0022		,	-1		,	-1		,	-1		}	, // TODO SGCap
		/*	29	*/	{	0	,	0x003b		,	0x007e		,	-1		,	-1		,	-1		}	, // TODO SGCap
		/*	2a	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	2b	*/	{	0	,	0x005c		,	0x007c		,	0x001c	,	-1		,	-1		}	, // TODO SGCap
		/*	2c	*/	{	1	,	0x05d6		,	(int)'Z'	,	-1		,	-1		,	-1		}	,
		/*	2d	*/	{	1	,	0x05e1		,	(int)'X'	,	-1		,	-1		,	-1		}	,
		/*	2e	*/	{	1	,	0x05d1		,	(int)'C'	,	-1		,	-1		,	-1		}	,
		/*	2f	*/	{	1	,	0x05d4		,	(int)'V'	,	-1		,	-1		,	-1		}	,
		
		/*	30	*/	{	1	,	0x05e0		,	(int)'B'	,	-1		,	-1		,	-1		}	,
		/*	31	*/	{	1	,	0x05de		,	(int)'N'	,	-1		,	-1		,	-1		}	,
		/*	32	*/	{	1	,	0x05e6		,	(int)'M'	,	-1		,	-1		,	-1		}	,
		/*	33	*/	{	0	,	0x05ea		,	0x003e		,	-1		,	-1		,	-1		}	, // TODO SGCap
		/*	34	*/	{	0	,	0x05e5		,	0x003c		,	-1		,	-1		,	-1		}	, // TODO SGCap
		/*	35	*/	{	0	,	0x002e		,	0x003f		,	-1		,	-1		,	-1		}	, // TODO SGCap
		/*	36	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	37	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	38	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	39	*/	{	0	,	0x0020		,	0x0020		,	0x0020	,	-1		,	-1		}	,
		/*	3a	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	3b	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	3c	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	3d	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	3e	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	3f	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		
		/*	40	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	41	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	42	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	43	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	44	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	45	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	46	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	47	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	48	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	49	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	4a	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	4b	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	4c	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	4d	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	4e	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	4f	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		
		/*	50	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	51	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	52	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	53	*/	{	0	,	0x002e		,	0x002e		,	-1		,	-1		,	-1		}	,	
		/*	54	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	55	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	56	*/	{	0	,	0x005c		,	0x007c		,	0x001c	,	-1		,	-1		}	,		
		/*	57	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	58	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	59	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	5a	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	5b	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	5c	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	5d	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	5e	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		/*	5f	*/	{	-1	,	0			,	0			,	0		,	0		,	0		}	,
		
	};
	
	public static final int DEADKEYS[] = null;
	public static final int DEADKEY_LUT[][] = null;
	
	private static HebrewLayout instance = new HebrewLayout();
	
	private HebrewLayout() {
	}
	
	public static HebrewLayout getInstance() {
		return instance;
	}	

	@Override
	public int[][] getLUT() {
		return LUT;
	}

	@Override
	public void type(String text) {
		super.type(LUT, DEADKEY_LUT, DEADKEYS, text, (byte)0);
	}	
	
	@Override
	public void type(String text, byte modifiers) {
		super.type(LUT, DEADKEY_LUT, DEADKEYS, text, modifiers);
	}	
	
	@Override
	public char getChar(int scanCode, boolean capsLock, boolean shift, boolean altGr) {
		return super.getChar(LUT, scanCode, capsLock, shift, altGr);
	}	
	
	@Override
	public String getLocaleName() {		
		return LOCALE_NAME;
	}	
	
	@Override
	public int[][] getDeadkeyLUT() {		
		return DEADKEY_LUT;
	}

	@Override
	public int[] getDeadkeys() {
		return DEADKEYS;
	}

}
