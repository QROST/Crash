﻿using BidirectionalMap;

using Crash.Common.Document;
using Crash.Common.Events;

using Rhino;
using Rhino.DocObjects;

namespace Crash.Handlers
{
	/// <summary>
	///     Contains pairings of RhinoDocs and CrashDocs
	/// </summary>
	public static class CrashDocRegistry
	{
		private static readonly BiMap<RhinoDoc, CrashDoc> s_documentRelationship;

		static CrashDocRegistry()
		{
			s_documentRelationship = new BiMap<RhinoDoc, CrashDoc>();
		}

		public static CrashDoc? GetRelatedDocument(RhinoDoc doc)
		{
			if (doc is null)
			{
				return null;
			}

			if (s_documentRelationship.Forward.ContainsKey(doc))
			{
				return s_documentRelationship.Forward[doc];
			}

			return null;
		}

		public static RhinoDoc? GetRelatedDocument(CrashDoc doc)
		{
			foreach (var kvp in s_documentRelationship.Reverse)
			{
				if (kvp.Key.Equals(doc))
				{
					return kvp.Value;
				}
			}

			return null;
		}

		public static IEnumerable<CrashDoc> GetOpenDocuments()
		{
			return s_documentRelationship.Forward.Values;
		}

		public static CrashDoc CreateAndRegisterDocument(RhinoDoc rhinoDoc)
		{
			if (s_documentRelationship.Forward.ContainsKey(rhinoDoc))
			{
				return s_documentRelationship.Forward[rhinoDoc];
			}

			var crashDoc = new CrashDoc();
			Register(crashDoc, rhinoDoc);
			DocumentRegistered?.Invoke(null, new CrashEventArgs(crashDoc));

			crashDoc.Queue.OnCompletedQueue += RedrawOncompleted;
			crashDoc.LocalClient.OnInit += RegisterQueue;

			return crashDoc;
		}

		private static void RegisterQueue(object? sender, CrashInitArgs e)
		{
			e.CrashDoc.LocalClient.OnInit -= RegisterQueue;
			RhinoApp.WriteLine("Loading Changes ...");

			EventHandler cycleQueueDelegate = null;
			cycleQueueDelegate = (o, args) =>
			                     {
				                     e.CrashDoc.Queue.RunNextAction();
			                     };
			RhinoApp.Idle += cycleQueueDelegate;

			EventHandler<CrashEventArgs> deRegisterQueueCycle = null;
			deRegisterQueueCycle = (o, args) =>
			                       {
				                       DocumentDisposed -= deRegisterQueueCycle;
				                       RhinoApp.Idle -= cycleQueueDelegate;
			                       };
			
			DocumentDisposed += deRegisterQueueCycle;
		}

		private static void CycleQueue(object sender, EventArgs e)
		{
		}

		private static void RedrawOncompleted(object? sender, CrashEventArgs e)
		{
			var rhinoDoc = GetRelatedDocument(e.CrashDoc);
			rhinoDoc.Views.Redraw();
		}

		private static void Register(CrashDoc crashDoc,
			RhinoDoc rhinoDoc)
		{
			s_documentRelationship.Add(rhinoDoc, crashDoc);
		}

		public static async Task DisposeOfDocumentAsync(CrashDoc crashDoc)
		{
			crashDoc.Queue.ForceCycleQueue();
			DocumentDisposed?.Invoke(null, new CrashEventArgs(crashDoc));
			// DeRegister Events
			crashDoc.Queue.OnCompletedQueue -= RedrawOncompleted;
			if (crashDoc.LocalClient is not null)
			{
				await crashDoc.LocalClient?.StopAsync();
			}

			// Remove Geometry
			var rhinoDoc = GetRelatedDocument(crashDoc);
			s_documentRelationship.Remove(rhinoDoc);

			var settings = new ObjectEnumeratorSettings
			               {
				               ActiveObjects = false, LockedObjects = true, HiddenObjects = true
			               };
			var rhinoObjects = rhinoDoc.Objects.GetObjectList(settings);
			foreach (var rhinoObject in rhinoObjects)
			{
				rhinoDoc.Objects.Unlock(rhinoObject, true);
				rhinoDoc.Objects.Show(rhinoObject, true);
			}

			rhinoDoc.Objects.Clear();

			// Dispose
			crashDoc?.Dispose();
		}

		public static event EventHandler<CrashEventArgs> DocumentRegistered;
		public static event EventHandler<CrashEventArgs> DocumentDisposed;
	}
}
