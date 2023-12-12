﻿using Crash.Common.Document;
using Crash.Handlers.Changes;
using Crash.Handlers.InternalEvents;
using Crash.Utils;

using Rhino.Geometry;

namespace Crash.Handlers.Plugins.Geometry.Create
{
	/// <summary>Captures Creation of default Rhino Geometry</summary>
	internal sealed class GeometryCreateAction : IChangeCreateAction
	{
		public ChangeAction Action => ChangeAction.Add | ChangeAction.Temporary;

		public bool CanConvert(object sender, CreateRecieveArgs crashArgs)
		{
			return crashArgs.Args is CrashObjectEventArgs rargs &&
			       rargs.Geometry is not null;
		}

		public bool TryConvert(object sender, CreateRecieveArgs crashArgs, out IEnumerable<Change> changes)
		{
			if (crashArgs.Args is not CrashObjectEventArgs cargs)
			{
				changes = Array.Empty<Change>();
				return false;
			}

			changes = CreateChangesFromArgs(crashArgs.Doc, cargs.RhinoId, cargs.Geometry, cargs.UnDelete);
			return changes.Any();
		}

		private IEnumerable<Change> CreateChangesFromArgs(CrashDoc crashDoc, Guid rhinoId, GeometryBase geometry,
			bool unDelete)
		{
			var rhinoDoc = CrashDocRegistry.GetRelatedDocument(crashDoc);
			if (rhinoDoc is null)
			{
				throw new NullReferenceException("Rhino Document cannot be found!");
			}

			var rhinoObject = rhinoDoc.Objects.FindId(rhinoId);
			if (rhinoObject is null)
			{
				return Array.Empty<Change>();
			}

			var user = crashDoc.Users.CurrentUser.Name;

			// For unDelete
			var currentOrNewId = Guid.NewGuid();
			if (crashDoc.RealisedChangeTable.TryGetChangeId(rhinoId, out var changeId))
			{
				currentOrNewId = changeId;
			}

			crashDoc.RealisedChangeTable.AddPair(currentOrNewId, rhinoId);

			Change change = null;
			if (unDelete)
			{
				change = GeometryChange.CreateChange(currentOrNewId, user, ChangeAction.Add);
			}
			else
			{
				change = GeometryChange.CreateChange(currentOrNewId, user, Action, geometry);
			}

			rhinoObject.SyncHost(change, crashDoc);

			return new[] { change };
		}
	}
}
