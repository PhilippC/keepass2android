package com.jcraft.jsch;

import static com.jcraft.jsch.Session.SSH_MSG_CHANNEL_OPEN;

/**
 * Extension of {@link ChannelDirectTCPIP} to support socket forwarding.
 * <p>
 * https://raw.githubusercontent.com/openssh/openssh-portable/master/PROTOCOL
 */
public class ChannelDirectStreamLocal extends ChannelDirectTCPIP {

    static private final int LOCAL_WINDOW_SIZE_MAX = 0x20000;
    static private final int LOCAL_MAXIMUM_PACKET_SIZE = 0x4000;
    static private final byte[] _type = Util.str2byte("direct-streamlocal@openssh.com");

    private String socketPath;

    ChannelDirectStreamLocal() {
        super();
        type = _type;
        setLocalWindowSizeMax(LOCAL_WINDOW_SIZE_MAX);
        setLocalWindowSize(LOCAL_WINDOW_SIZE_MAX);
        setLocalPacketSize(LOCAL_MAXIMUM_PACKET_SIZE);
    }

    @Override
    protected Packet genChannelOpenPacket() {

        if (socketPath == null) {
            session.getLogger().log(Logger.FATAL, "socketPath must be set");
            throw new RuntimeException("socketPath must be set");
        }

        /*
        Similar to direct-tcpip, direct-streamlocal is sent by the client
        to request that the server make a connection to a Unix domain socket.

            byte      SSH_MSG_CHANNEL_OPEN
            string    "direct-streamlocal@openssh.com"
            uint32    sender channel
            uint32    initial window size
            uint32    maximum packet size
            string    socket path
            string    reserved
            uint32    reserved
         */

        Buffer buf = new Buffer(50 +
                socketPath.length() +
                Session.buffer_margin);
        Packet packet = new Packet(buf);
        packet.reset();
        buf.putByte((byte) SSH_MSG_CHANNEL_OPEN);
        buf.putString(this.type);
        buf.putInt(id);
        buf.putInt(lwsize);
        buf.putInt(lmpsize);
        buf.putString(Util.str2byte(socketPath));
        buf.putString(Util.str2byte(originator_IP_address));
        buf.putInt(originator_port);
        return packet;
    }

    public String getSocketPath() {
        return socketPath;
    }

    public void setSocketPath(String socketPath) {
        this.socketPath = socketPath;
    }
}
