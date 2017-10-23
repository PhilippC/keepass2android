package keepass2android.javafilestorage.webdav;

import android.util.Xml;

import org.xmlpull.v1.XmlPullParser;
import org.xmlpull.v1.XmlPullParserException;

import java.io.IOException;
import java.io.Reader;
import java.net.MalformedURLException;
import java.net.URL;
import java.util.ArrayList;
import java.util.List;

/**
 * Created by Philipp on 21.09.2016.
 */
public class PropfindXmlParser
{
    private final String ns = "DAV:";

    public static class Response
    {
        public Response()
        {
            propstat = new ArrayList<PropStat>();
        }

        public String href;

        public URL getAbsoluteUri(URL requestUrl) throws MalformedURLException {
            String serverUrl = requestUrl.getProtocol() + "://" + requestUrl.getHost();
            if (requestUrl.getPort() > 0)
                serverUrl += ":" + requestUrl.getPort();
            return new URL(new URL(serverUrl),href);
        }

        public static class PropStat {

            public PropStat()
            {
                prop = new Prop();
            }


            public boolean isOk()
            {
                if (status == null)
                    return false;
                String[] parts = status.split(" ");
                if (parts.length < 2)
                    return false;
                return parts[1].equals("200");
            }

            public static class Prop {
                public String DisplayName;
                public String LastModified;
                public String ContentLength;
            }
            public String status;
            public Prop prop;
        }

        ArrayList<PropStat> propstat;

        public PropStat.Prop getOkProp()
        {
            for (PropStat p: propstat)
            {
                if (p.isOk())
                    return p.prop;
            }
            return null;
        }

    }

    public List<Response> parse(Reader in) throws XmlPullParserException, IOException {
        List<Response> responses = new ArrayList<Response>();

        XmlPullParser parser = Xml.newPullParser();

        parser.setFeature(XmlPullParser.FEATURE_PROCESS_NAMESPACES,true);
        parser.setInput(in);
        parser.nextTag();

        parser.require(XmlPullParser.START_TAG, ns, "multistatus");


        while (parser.next() != XmlPullParser.END_TAG) {
            android.util.Log.d("PARSE", "1eventtype=" + parser.getEventType());

            if (parser.getEventType() != XmlPullParser.START_TAG) {
                continue;
            }
            String name = parser.getName();

            android.util.Log.d("PARSE", "1name = " + name);

            // Starts by looking for the entry tag
            if (name.equals("response")) {
                responses.add(readResponse(parser));
            } else {
                skip(parser);
            }
        }


        return responses;
    }

    private Response readResponse(XmlPullParser parser) throws IOException, XmlPullParserException {
        Response response = new Response();

        parser.require(XmlPullParser.START_TAG, ns, "response");

        android.util.Log.d("PARSE", "readResponse");

        while (parser.next() != XmlPullParser.END_TAG) {
            android.util.Log.d("PARSE", "2eventtype=" + parser.getEventType());

            if (parser.getEventType() != XmlPullParser.START_TAG) {
                continue;
            }
            String name = parser.getName();


            android.util.Log.d("PARSE", "2name=" + name);


            if (name.equals("href")) {
                response.href = readText(parser);
            } else if (name.equals("propstat")) {
                response.propstat.add(readPropStat(parser));
            } else {
                skip(parser);
            }
        }
        return response;

    }


    // For the tags title and summary, extracts their text values.
    private String readText(XmlPullParser parser) throws IOException, XmlPullParserException {
        String result = "";
        if (parser.next() == XmlPullParser.TEXT) {
            result = parser.getText();
            parser.nextTag();
        }
        android.util.Log.d("PARSE", "read text " + result);
        return result;
    }


    private Response.PropStat readPropStat(XmlPullParser parser) throws IOException, XmlPullParserException {
        Response.PropStat propstat = new Response.PropStat();
        parser.require(XmlPullParser.START_TAG, ns, "propstat");
        android.util.Log.d("PARSE", "readPropStat");
        while (parser.next() != XmlPullParser.END_TAG) {

            android.util.Log.d("PARSE", "3eventtype=" + parser.getEventType());

            if (parser.getEventType() != XmlPullParser.START_TAG) {
                continue;
            }
            String name = parser.getName();
            android.util.Log.d("PARSE", "3name=" + name);
            if (name.equals("prop"))
            {
                propstat.prop = readProp(parser);
            } else if (name.equals("status"))
            {
                propstat.status = readText(parser);
            } else {
                skip(parser);
            }
        }
        return propstat;
    }

    private Response.PropStat.Prop readProp(XmlPullParser parser) throws IOException, XmlPullParserException {
        Response.PropStat.Prop prop = new Response.PropStat.Prop();
        parser.require(XmlPullParser.START_TAG, ns, "prop");
        android.util.Log.d("PARSE", "readProp");
        while (parser.next() != XmlPullParser.END_TAG) {
            android.util.Log.d("PARSE", "eventtype=" + parser.getEventType());

            if (parser.getEventType() != XmlPullParser.START_TAG) {
                continue;
            }
            String name = parser.getName();

            android.util.Log.d("PARSE", "4name = " + name);
            if (name.equals("getcontentlength"))
            {
                prop.ContentLength = readText(parser);
            } else if (name.equals("getlastmodified")) {
                prop.LastModified = readText(parser);
            } else if (name.equals("displayname")) {
                prop.DisplayName = readText(parser);
            } else {
                skip(parser);
            }
        }

        return  prop;
    }

    private void skip(XmlPullParser parser) throws XmlPullParserException, IOException {
        android.util.Log.d("PARSE", "skipping " + parser.getName());

        if (parser.getEventType() != XmlPullParser.START_TAG) {
            throw new IllegalStateException();
        }
        int depth = 1;
        while (depth != 0) {
            switch (parser.next()) {
                case XmlPullParser.END_TAG:
                    depth--;
                    break;
                case XmlPullParser.START_TAG:
                    depth++;
                    break;
            }
        }
    }
}

