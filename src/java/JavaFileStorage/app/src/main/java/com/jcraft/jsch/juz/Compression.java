/* -*-mode:java; c-basic-offset:2; indent-tabs-mode:nil -*- */
package com.jcraft.jsch.juz;

import com.jcraft.jsch.*;
import java.util.function.Supplier;
import java.util.zip.Deflater;
import java.util.zip.Inflater;

/**
 * This example demonstrates the packet compression without using jzlib[1].
 *
 * The ssh protocol adopts zlib[2] for the packet compression.  Fortunately,
 * JDK has provided wrapper classes for zlib(j.u.z.{Deflater, Inflater}),
 * but it does not expose enough functionality of zlib, unfortunately;
 * it must not allow to compress data with SYNC_FLUSH.  So, JSch has been
 * using jzlib by the default.  After 12 years of bug parade entry[3] filing,
 * Java7 has revised j.u.z.Deflater, and SYNC_FLUSH has been supported at last.
 * This example shows how to enable the packet compression by using JDK's
 * java.util.zip package.
 *
 *
 * [1] http://www.jcraft.com/jzlib/
 * [2] http://www.zlib.net/
 * [3] https://bugs.openjdk.java.net/browse/JDK-4206909
 */
public class Compression implements com.jcraft.jsch.Compression {
  static private final int BUF_SIZE=4096;
  private final int buffer_margin=32+20; // AES256 + HMACSHA1
  private Deflater deflater;
  private Inflater inflater;
  private byte[] tmpbuf=new byte[BUF_SIZE];
  private byte[] inflated_buf;
  private Session session;

  public Compression(){
  }

  private void logMessage(int level, Supplier<String> message) {
    Logger logger = session == null ? JSch.getLogger() : session.getLogger();
    if (!logger.isEnabled(level)) {
      return;
    }
    logger.log(level, message.get());
  }

  @Override
  public void end() {
    inflated_buf=null;
    if(inflater!=null){
      inflater.end();
      inflater=null;
    }
    if(deflater!=null){
      deflater.end();
      deflater=null;
    }
    session=null;
  }

  @Override
  public void init(int type, int level, Session session){
    this.session = session;
    init(type, level);
  }

  @Override
  public void init(int type, int level){
    if(type==DEFLATER){
      deflater=new Deflater(level);
    }
    else if(type==INFLATER){
      inflater=new Inflater();
      inflated_buf=new byte[BUF_SIZE];
    }
    logMessage(Logger.DEBUG, () -> "zlib using "+this.getClass().getCanonicalName());
  }

  @Override
  public byte[] compress(byte[] buf, int start, int[] end){

    // There may be a bug in j.u.z.Deflater.
    // It seems to me that if the size of buffer for Deflater#deflate() is
    // not enough, that method will return weird value ;-(
    if(tmpbuf.length<end[0]){
      tmpbuf = new byte[end[0]*2];
    }

    deflater.setInput(buf, start, end[0]-start);

    byte[] obuf = buf;   // output buffer
    int obuflen = start; // length of output buffer
    do {
      int result = deflater.deflate(tmpbuf, 0, tmpbuf.length,
                                    Deflater.SYNC_FLUSH);
      // deflation of delfated data may inflate it.
      if(obuf.length < obuflen + result + buffer_margin){
        byte[] tmp=new byte[(obuflen + result + buffer_margin)*2];
        System.arraycopy(obuf, 0, tmp, 0, obuf.length);
        obuf=tmp;
      }
      System.arraycopy(tmpbuf, 0, obuf, obuflen, result);
      obuflen+=result;
    }
    while(!deflater.needsInput());

    end[0] = obuflen;
    return obuf;
  }

  @Override
  public byte[] uncompress(byte[] buf, int start, int[] len){
    inflater.setInput(buf, start, len[0]);

    int inflated_end = 0;
    try{
      do {
        int result = inflater.inflate(tmpbuf, 0, tmpbuf.length);
        if(inflated_buf.length < inflated_end + result){
          byte[] tmp = new byte[inflated_end + result];
          System.arraycopy(inflated_buf, 0, tmp, 0, inflated_end);
          inflated_buf = tmp;
        }
        System.arraycopy(tmpbuf, 0, inflated_buf, inflated_end, result);
        inflated_end +=result;
      }
      while(inflater.getRemaining()>0);
    }
    catch(java.util.zip.DataFormatException e){
      logMessage(Logger.WARN, () -> "an exception during uncompress\n"+e.toString());
    }

    if(buf.length<inflated_buf.length+start){
      byte[] tmp = new byte[inflated_buf.length+start];
      System.arraycopy(buf, 0, tmp, 0, start);
      buf=tmp;
    }
    System.arraycopy(inflated_buf, 0, buf, start, inflated_end);
    len[0]=inflated_end;
    return buf;
  }
}
