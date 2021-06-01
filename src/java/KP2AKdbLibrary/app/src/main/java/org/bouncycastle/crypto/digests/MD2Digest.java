package org.bouncycastle.crypto.digests;

import org.bouncycastle.crypto.*;
/**
 * implementation of MD2
 * as outlined in RFC1319 by B.Kaliski from RSA Laboratories April 1992
 */
public class MD2Digest
    implements ExtendedDigest
{
    private static final int DIGEST_LENGTH = 16;

    /* X buffer */
    private byte[]   X = new byte[48];
    private int     xOff;
    /* M buffer */
    private byte[]   M = new byte[16];
    private int     mOff;
    /* check sum */
    private byte[]   C = new byte[16];
    private int COff;

    public MD2Digest()
    {
        reset();
    }
    public MD2Digest(MD2Digest t)
    {
        System.arraycopy(t.X, 0, X, 0, t.X.length);
        xOff = t.xOff;
        System.arraycopy(t.M, 0, M, 0, t.M.length);
        mOff = t.mOff;
        System.arraycopy(t.C, 0, C, 0, t.C.length);
        COff = t.COff;
    }
    /**
     * return the algorithm name
     *
     * @return the algorithm name
     */
    public String getAlgorithmName()
    {
        return "MD2";
    }
    /**
     * return the size, in bytes, of the digest produced by this message digest.
     *
     * @return the size, in bytes, of the digest produced by this message digest.
     */
    public int getDigestSize()
    {
        return DIGEST_LENGTH;
    }
    /**
     * close the digest, producing the final digest value. The doFinal
     * call leaves the digest reset.
     *
     * @param out the array the digest is to be copied into.
     * @param outOff the offset into the out array the digest is to start at.
     */
    public int doFinal(byte[] out, int outOff)
    {
        // add padding
        byte paddingByte = (byte)(M.length-mOff);
        for (int i=mOff;i<M.length;i++)
        {
            M[i] = paddingByte;
        }
        //do final check sum
        processCheckSum(M);
        // do final block process
        processBlock(M);

        processBlock(C);

        System.arraycopy(X,xOff,out,outOff,16);

        reset();

        return DIGEST_LENGTH;
    }
    /**
     * reset the digest back to it's initial state.
     */
    public void reset()
    {
        xOff = 0;
        for (int i = 0; i != X.length; i++)
        {
            X[i] = 0;
        }
        mOff = 0;
        for (int i = 0; i != M.length; i++)
        {
            M[i] = 0;
        }
        COff = 0;
        for (int i = 0; i != C.length; i++)
        {
            C[i] = 0;
        }
    }
    /**
     * update the message digest with a single byte.
     *
     * @param in the input byte to be entered.
     */
    public void update(byte in)
    {
        M[mOff++] = in;

        if (mOff == 16)
        {
            processCheckSum(M);
            processBlock(M);
            mOff = 0;
        }
    }

    /**
     * update the message digest with a block of bytes.
     *
     * @param in the byte array containing the data.
     * @param inOff the offset into the byte array where the data starts.
     * @param len the length of the data.
     */
    public void update(byte[] in, int inOff, int len)
    {
        //
        // fill the current word
        //
        while ((mOff != 0) && (len > 0))
        {
            update(in[inOff]);
            inOff++;
            len--;
        }

        //
        // process whole words.
        //
        while (len > 16)
        {
            System.arraycopy(in,inOff,M,0,16);
            processCheckSum(M);
            processBlock(M);
            len -= 16;
            inOff += 16;
        }

        //
        // load in the remainder.
        //
        while (len > 0)
        {
            update(in[inOff]);
            inOff++;
            len--;
        }
    }
    protected void processCheckSum(byte[] m)
    {
        int L = C[15];
        for (int i=0;i<16;i++)
        {
            C[i] ^= S[(m[i] ^ L) & 0xff];
            L = C[i];
        }
    }
    protected void processBlock(byte[] m)
    {
        for (int i=0;i<16;i++)
        {
            X[i+16] = m[i];
            X[i+32] = (byte)(m[i] ^ X[i]);
        }
        // encrypt block
        int t = 0;

        for (int j=0;j<18;j++)
        {
            for (int k=0;k<48;k++)
            {
                t = X[k] ^= S[t];
                t = t & 0xff;
            }
            t = (t + j)%256;
        }
     }
     // 256-byte random permutation constructed from the digits of PI
    private static final byte[] S = {
      (byte)41,(byte)46,(byte)67,(byte)201,(byte)162,(byte)216,(byte)124,
      (byte)1,(byte)61,(byte)54,(byte)84,(byte)161,(byte)236,(byte)240,
      (byte)6,(byte)19,(byte)98,(byte)167,(byte)5,(byte)243,(byte)192,
      (byte)199,(byte)115,(byte)140,(byte)152,(byte)147,(byte)43,(byte)217,
      (byte)188,(byte)76,(byte)130,(byte)202,(byte)30,(byte)155,(byte)87,
      (byte)60,(byte)253,(byte)212,(byte)224,(byte)22,(byte)103,(byte)66,
      (byte)111,(byte)24,(byte)138,(byte)23,(byte)229,(byte)18,(byte)190,
      (byte)78,(byte)196,(byte)214,(byte)218,(byte)158,(byte)222,(byte)73,
      (byte)160,(byte)251,(byte)245,(byte)142,(byte)187,(byte)47,(byte)238,
      (byte)122,(byte)169,(byte)104,(byte)121,(byte)145,(byte)21,(byte)178,
      (byte)7,(byte)63,(byte)148,(byte)194,(byte)16,(byte)137,(byte)11,
      (byte)34,(byte)95,(byte)33,(byte)128,(byte)127,(byte)93,(byte)154,
      (byte)90,(byte)144,(byte)50,(byte)39,(byte)53,(byte)62,(byte)204,
      (byte)231,(byte)191,(byte)247,(byte)151,(byte)3,(byte)255,(byte)25,
      (byte)48,(byte)179,(byte)72,(byte)165,(byte)181,(byte)209,(byte)215,
      (byte)94,(byte)146,(byte)42,(byte)172,(byte)86,(byte)170,(byte)198,
      (byte)79,(byte)184,(byte)56,(byte)210,(byte)150,(byte)164,(byte)125,
      (byte)182,(byte)118,(byte)252,(byte)107,(byte)226,(byte)156,(byte)116,
      (byte)4,(byte)241,(byte)69,(byte)157,(byte)112,(byte)89,(byte)100,
      (byte)113,(byte)135,(byte)32,(byte)134,(byte)91,(byte)207,(byte)101,
      (byte)230,(byte)45,(byte)168,(byte)2,(byte)27,(byte)96,(byte)37,
      (byte)173,(byte)174,(byte)176,(byte)185,(byte)246,(byte)28,(byte)70,
      (byte)97,(byte)105,(byte)52,(byte)64,(byte)126,(byte)15,(byte)85,
      (byte)71,(byte)163,(byte)35,(byte)221,(byte)81,(byte)175,(byte)58,
      (byte)195,(byte)92,(byte)249,(byte)206,(byte)186,(byte)197,(byte)234,
      (byte)38,(byte)44,(byte)83,(byte)13,(byte)110,(byte)133,(byte)40,
      (byte)132, 9,(byte)211,(byte)223,(byte)205,(byte)244,(byte)65,
      (byte)129,(byte)77,(byte)82,(byte)106,(byte)220,(byte)55,(byte)200,
      (byte)108,(byte)193,(byte)171,(byte)250,(byte)36,(byte)225,(byte)123,
      (byte)8,(byte)12,(byte)189,(byte)177,(byte)74,(byte)120,(byte)136,
      (byte)149,(byte)139,(byte)227,(byte)99,(byte)232,(byte)109,(byte)233,
      (byte)203,(byte)213,(byte)254,(byte)59,(byte)0,(byte)29,(byte)57,
      (byte)242,(byte)239,(byte)183,(byte)14,(byte)102,(byte)88,(byte)208,
      (byte)228,(byte)166,(byte)119,(byte)114,(byte)248,(byte)235,(byte)117,
      (byte)75,(byte)10,(byte)49,(byte)68,(byte)80,(byte)180,(byte)143,
      (byte)237,(byte)31,(byte)26,(byte)219,(byte)153,(byte)141,(byte)51,
      (byte)159,(byte)17,(byte)131,(byte)20
    };

   public int getByteLength() 
   {
      return 16;
   }
}


