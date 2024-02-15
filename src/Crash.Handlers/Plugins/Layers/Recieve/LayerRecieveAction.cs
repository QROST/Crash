﻿using System.Text.Json;

using Crash.Changes.Extensions;
using Crash.Common.Document;
using Crash.Common.Events;
using Crash.Events;

using Rhino.DocObjects;

namespace Crash.Handlers.Plugins.Layers.Recieve
{
	public class LayerRecieveAction : IChangeRecieveAction
	{
		public bool CanRecieve(IChange change)
		{
			return change.HasFlag(ChangeAction.Add);
		}

		public async Task OnRecieveAsync(CrashDoc crashDoc, Change recievedChange)
		{
			crashDoc.Queue.AddAction(new IdleAction(CreateLayerAction, new IdleArgs(crashDoc, recievedChange)));
		}

		private void CreateLayerAction(IdleArgs args)
		{
			var packet = JsonSerializer.Deserialize<PayloadPacket>(args.Change.Payload);
			var layerUpdates = packet.Updates;

			var rhinoDoc = CrashDocRegistry.GetRelatedDocument(args.Doc);

			if (!layerUpdates.TryGetValue(nameof(Layer.FullPath), out var fullPath))
			{
				return;
			}

			var layerIndex = rhinoDoc.Layers.FindByFullPath(fullPath, -1);
			var layer = rhinoDoc.Layers.FindIndex(layerIndex);
			if (layer is null)
			{
				layer = new Layer();
			}

			RhinoLayerUtils.UpdateLayer(layer, layerUpdates);

			if (!layer.HasIndex)
			{
				rhinoDoc.Layers.Add(layer);
			}
		}
	}
}
