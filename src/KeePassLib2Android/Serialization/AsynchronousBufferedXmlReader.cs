using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Xml;
using System.Diagnostics;

namespace KeePassLib.Serialization
{
	public class AsynchronousBufferedXmlReader : XmlReader
	{
		/// <summary>
		/// An element which indicates the end of the XML document has been reached.
		/// </summary>
		private static readonly Element EndMarker = new Element();

		/// <summary>
		/// The next buffered element available for reading.
		/// Volatility: only read/written to by non-buffering thread. Passed to the buffer thread as an initial parameter.
		/// </summary>
		Element mBufferQueueHead = new Element(); // Start off with the pre-document element. No content, yet.

		private readonly Thread mWorkerThread;
		private readonly AutoResetEvent mWaitForBuffer = new AutoResetEvent(false);
		/// <summary>
		/// True while the reader thread is stalled waiting for buffering.
		/// Volaitlity: Only written by read thread. Only read by buffer thread
		/// </summary>
		private volatile bool mWaitingForBuffer;

#if TRACE
		private Stopwatch mReadWaitTimer = new Stopwatch();
		private Stopwatch mBufferCompletedTimer = new Stopwatch();
#endif

		/// <summary>
		/// Testing helper method
		/// </summary>
		/// <param name="input"></param>
		/// <returns></returns>
		public static XmlReader FullyBuffer(Stream input)
		{
			var reader = new AsynchronousBufferedXmlReader();
			reader.ReadStreamWorker(input);
			return reader;
		}

		private AsynchronousBufferedXmlReader()
		{
			// Once the end is reached, it stays there.
			EndMarker.NextElement = EndMarker;
		}

		public AsynchronousBufferedXmlReader(Stream input) : this()
		{
			mWorkerThread = new Thread(ReadStreamWorker) { Name = GetType().Name };
			mWorkerThread.Start(input);
		}

		#region Buffering
		private void ReadStreamWorker(object state)
		{
			var input = (Stream)state;

			var xr = XmlReader.Create(input, KdbxFile.CreateStdXmlReaderSettings());

			/// <summary>
			/// The last buffered element available for reading.
			/// </summary>
			Element bufferQueueTail = mBufferQueueHead;

			/// <summary>
			/// The element currently being buffered. Not yet available for reading.
			/// </summary>
			Element currentElement = null;

			while (xr.Read())
			{
				switch (xr.NodeType)
				{
					case XmlNodeType.Element:
						// Start a new element
						if (currentElement != null)
						{
							// Add the previous current element to the tail of the buffer
							bufferQueueTail.NextElement = currentElement;
							bufferQueueTail = currentElement;
							if (mWaitingForBuffer) mWaitForBuffer.Set(); // Signal that a new element is available in the buffer
						}

						currentElement = new Element { Name = xr.Name };

						// Process attributes - current optimisation, all elements have 0 or 1 attribute
						if (xr.MoveToNextAttribute())
						{
#if DEBUG
							Debug.Assert(xr.AttributeCount == 1);
							currentElement.AttributeName = xr.Name;
#endif
							currentElement.AttributeValue = xr.Value;
						}

						currentElement.IsEmpty = xr.IsEmptyElement;

						break;

					case XmlNodeType.Text:
						currentElement.Value = xr.Value;
						currentElement.IsEmpty = true; // Mark as empty because it will have no end element written for it
						break;

					case XmlNodeType.EndElement:
						Debug.Assert(currentElement != null, "Ending an element that was never started");
						
						// If this is an element with children (not one with a value) add an end element marker to the queue
						if (currentElement.Value == null || currentElement.Name != xr.Name)
						{
							bufferQueueTail.NextElement = currentElement;
							bufferQueueTail = currentElement;
							if (mWaitingForBuffer) mWaitForBuffer.Set(); // Signal that a new element is available in the buffer

							currentElement = new Element { Name = xr.Name, IsEndElement = true };
						}
						break;
				}
			}

			// Conclude the document, add the final element to the buffer and mark the ending
			currentElement.NextElement = EndMarker;
			bufferQueueTail.NextElement = currentElement;
			bufferQueueTail = currentElement;
			mWaitForBuffer.Set(); // Signal that final element is available in the buffer (regardless of wait flag, to avoid race condition)
#if TRACE
			mBufferCompletedTimer.Start();
#endif
		}
		#endregion

		private class Element
		{
			/// <summary>
			/// Link to the next buffered element.
			/// Volatility: Written to by buffer thread only. Read by both threads
			/// </summary>
			public volatile Element NextElement;
			
			public string Name;
			
			/// <summary>
			/// If this element marks the end of an xml element with child nodes, the IsEndElement will be true, and Value must be null.
			/// </summary>
			public bool IsEndElement;

			/// <summary>
			/// Set true if this represents an empty element
			/// </summary>
			public bool IsEmpty;

			/// <summary>
			/// If Value is non-null, then there will be no corresponding Element with IsEndElement created.
			/// </summary>
			public string Value;

			// Currently KDBX has a maximum of one attribute per element, so no need for a dictionary here, and the name is only used for debug asserts
#if DEBUG
			public string AttributeName;
#endif
			public string AttributeValue;
		}

		#region Custom XmlReader implementation for usage by KdbxFile only
		public override bool Read()
		{
			Element nextElement;
			while ((nextElement = mBufferQueueHead.NextElement) == null)
			{
#if TRACE
				mReadWaitTimer.Start();
#endif
				mWaitingForBuffer = true;
				mWaitForBuffer.WaitOne();
				mWaitingForBuffer = false;

#if TRACE
				mReadWaitTimer.Stop();
#endif
			}
			mBufferQueueHead = mBufferQueueHead.NextElement;


#if TRACE
			if (mBufferQueueHead == EndMarker)
			{
				Debug.WriteLine(String.Format("Asynchronous Buffered XmlReader waited for a total of: {0}ms, buffer completed {1}ms ahead of read", mReadWaitTimer.ElapsedMilliseconds, mBufferCompletedTimer.ElapsedMilliseconds));
			}
#endif
			return mBufferQueueHead != EndMarker;
		}

		public override string ReadElementString()
		{
			var result = mBufferQueueHead.Value ?? String.Empty; // ReadElementString returns empty strings for null content
			Read(); // Read element string always skips to the start of the next element
			return result;
		}

		public override XmlNodeType NodeType
		{
			get 
			{
				return mBufferQueueHead.IsEndElement ? XmlNodeType.EndElement : XmlNodeType.Element;
			}
		}

		public override bool IsEmptyElement
		{
			get
			{
				return mBufferQueueHead.IsEmpty;
			}
		}

		public override string Name
		{
			get
			{
				return mBufferQueueHead.Name;
			}
		}

		public override bool HasAttributes
		{
			get
			{
				return mBufferQueueHead.AttributeValue != null;
			}
		}

		public override bool MoveToAttribute(string name)
		{
#if DEBUG
			Debug.Assert(mBufferQueueHead.AttributeName == name);
#endif

			return true;
		}

		public override string Value
		{
			get
			{
				return mBufferQueueHead.AttributeValue;
			}
		}


		public override bool MoveToElement()
		{
			return true;
		}
		#endregion

		#region Unimplemented XmlReader overrides

		public override int AttributeCount
		{
			get { throw new NotImplementedException(); }
		}

		public override string BaseURI
		{
			get { throw new NotImplementedException(); }
		}

		public override void Close()
		{
			throw new NotImplementedException();
		}

		public override int Depth
		{
			get { throw new NotImplementedException(); }
		}

		public override bool EOF
		{
			get { throw new NotImplementedException(); }
		}

		public override string GetAttribute(int i)
		{
			throw new NotImplementedException();
		}

		public override string GetAttribute(string name, string namespaceURI)
		{
			throw new NotImplementedException();
		}

		public override string GetAttribute(string name)
		{
			throw new NotImplementedException();
		}

		public override bool HasValue
		{
			get { throw new NotImplementedException(); }
		}

		public override string LocalName
		{
			get { throw new NotImplementedException(); }
		}

		public override string LookupNamespace(string prefix)
		{
			throw new NotImplementedException();
		}

		public override bool MoveToAttribute(string name, string ns)
		{
			throw new NotImplementedException();
		}

		public override bool MoveToFirstAttribute()
		{
			throw new NotImplementedException();
		}

		public override bool MoveToNextAttribute()
		{
			throw new NotImplementedException();
		}

		public override XmlNameTable NameTable
		{
			get { throw new NotImplementedException(); }
		}

		public override string NamespaceURI
		{
			get { throw new NotImplementedException(); }
		}

		public override string Prefix
		{
			get { throw new NotImplementedException(); }
		}

		public override bool ReadAttributeValue()
		{
			throw new NotImplementedException();
		}

		public override ReadState ReadState
		{
			get { throw new NotImplementedException(); }
		}

		public override void ResolveEntity()
		{
			throw new NotImplementedException();
		}
		#endregion
	}
}
