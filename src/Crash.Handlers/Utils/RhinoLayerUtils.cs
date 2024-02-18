﻿using System.Drawing;

using Rhino;
using Rhino.DocObjects;

namespace Crash.Handlers
{
	internal static class RhinoLayerUtils
	{
#if NETFRAMEWORK
		private static readonly string Separater = Layer.PathSeparator;
#else
		private static readonly string Separater = ModelComponent.NamePathSeparator;
#endif

		private const string KeyDivider = ";";

		private static Layer GetDefaultLayer()
		{
			return new Layer();
		}

		private static void EmptySetValue(Layer layer, object value) { }

		private static int GetIntOrDefault(string value)
		{
			if (int.TryParse(value, out var result))
			{
				return result;
			}

			return -1;
		}

		private static double GetDoubleOrDefault(string value)
		{
			if (double.TryParse(value, out var result))
			{
				return result;
			}

			return 0.0;
		}

		private static Color GetColourOrDefault(string value, Color defaultValue)
		{
			if (!int.TryParse(value, out var result))
			{
				return defaultValue;
			}

			return Color.FromArgb(result);
		}

		private static string SerializeColour(Color colour)
		{
			return colour.ToArgb().ToString();
		}

		private static bool GetBoolOrDefault(string value, bool defaultValue = true)
		{
			if (bool.TryParse(value, out var result))
			{
				return result;
			}

			return defaultValue;
		}

		private static string GetStringOrDefault(string value, string defaultValue)
		{
			if (string.IsNullOrEmpty(value))
			{
				return defaultValue ?? string.Empty;
			}

			return value;
		}

		static RhinoLayerUtils()
		{
			s_userSpecificKeys = new HashSet<string> { nameof(Layer.IsLocked), nameof(Layer.IsVisible) };
			s_gettersAndSetters = new Dictionary<string, GetterAndSetter>
			                      {
				                      {
					                      nameof(Layer.Name), new GetterAndSetter(layer => layer.Name,
						                      (layer, value) =>
						                      {
							                      layer.Name =
								                      GetStringOrDefault(value,
								                                         layer.FullPath
								                                              ?.Split(new[] { Separater },
									                                              StringSplitOptions
										                                              .RemoveEmptyEntries)
								                                              ?.Last());
						                      })
				                      },
				                      { nameof(Layer.FullPath), new(layer => layer.FullPath, EmptySetValue) },
				                      {
					                      nameof(Layer.Color),
					                      new GetterAndSetter(layer => SerializeColour(layer.Color),
					                                          (layer, value) =>
						                                          layer.Color = GetColourOrDefault(value, Color.Black))
				                      },
				                      {
					                      nameof(Layer.LinetypeIndex),
					                      new GetterAndSetter(layer => layer.LinetypeIndex.ToString(),
					                                          (layer, value) =>
						                                          layer.LinetypeIndex = GetIntOrDefault(value))
				                      },
				                      {
					                      nameof(Layer.PlotColor),
					                      new GetterAndSetter(layer => SerializeColour(layer.PlotColor),
					                                          (layer, value) =>
						                                          layer.Color = GetColourOrDefault(value, Color.Black)
					                                         )
				                      },
				                      {
					                      nameof(Layer.PlotWeight),
					                      new GetterAndSetter(layer => layer.PlotWeight.ToString(),
					                                          (layer, value) =>
						                                          layer.PlotWeight = GetDoubleOrDefault(value))
				                      },
				                      {
					                      nameof(Layer.RenderMaterial),
					                      new GetterAndSetter(layer => layer.RenderMaterial.DisplayName, EmptySetValue)
				                      },

				                      // User Specific
				                      {
					                      nameof(Layer.IsLocked),
					                      new GetterAndSetter(layer => layer.IsLocked.ToString(),
					                                          (layer, value) =>
						                                          layer.IsLocked = GetBoolOrDefault(value))
				                      },
				                      {
					                      nameof(Layer.IsVisible),
					                      new GetterAndSetter(layer => layer.IsVisible.ToString(),
					                                          (layer, value) =>
						                                          layer.IsVisible = GetBoolOrDefault(value))
				                      }
			                      };
		}

		private static Dictionary<string, GetterAndSetter> s_gettersAndSetters { get; }
		private static HashSet<string> s_userSpecificKeys { get; }

		internal static Dictionary<string, string> GetLayerDefaults(Layer layer, string userName)
		{
			return GetLayerDifference(GetDefaultLayer(), layer, userName);
		}

		internal static Dictionary<string, string> GetLayerDifference(Layer oldState, Layer newState, string userName)
		{
			var dict = new Dictionary<string, string>();

			foreach (var getter in s_gettersAndSetters)
			{
				if (!IsDifferent(getter.Value.Get, oldState, newState, out var oldValue, out var newValue))
				{
					continue;
				}

				dict.Add(GetOldKey(getter.Key, userName), oldValue);
				dict.Add(GetNewKey(getter.Key, userName), newValue);
			}

			var oldFullPathKey = GetOldKey(nameof(Layer.FullPath), userName);
			var newFullPathKey = GetNewKey(nameof(Layer.FullPath), userName);
			if (!dict.ContainsKey(oldFullPathKey))
			{
				dict.Add(oldFullPathKey, oldState.FullPath);
			}

			if (!dict.ContainsKey(newFullPathKey))
			{
				dict.Add(newFullPathKey, newState.FullPath);
			}

			return dict;
		}

		internal static string GetNewKey(string key, string userName)
		{
			return $"New{KeyDivider}{GetUserSpecificKey(key, userName)}{key}";
		}

		internal static string GetOldKey(string key, string userName)
		{
			return $"Old{KeyDivider}{GetUserSpecificKey(key, userName)}{key}";
		}

		internal static string GetUserSpecificKey(string key, string userName)
		{
			if (s_userSpecificKeys.Contains(key))
			{
				return $"{userName}{KeyDivider}";
			}

			return string.Empty;
		}

		private static string GetNeutralKey(string key, string userName)
		{
			var neutralKey = key.Replace("Old", "")
			                    .Replace("New", "")
			                    .Replace(KeyDivider, "")
			                    .Replace(userName, "");

			return neutralKey;
		}

		internal static bool TryGetAtExpectedPath(RhinoDoc rhinoDoc, Dictionary<string, string> layerUpdates,
			string userName, out Layer layer)
		{
			return TryGetLayer(GetNewKey(nameof(Layer.FullPath), userName), rhinoDoc, layerUpdates, out layer);
		}

		internal static bool TryGetAtOldPath(RhinoDoc rhinoDoc, Dictionary<string, string> layerUpdates,
			string userName, out Layer layer)
		{
			return TryGetLayer(GetOldKey(nameof(Layer.FullPath), userName), rhinoDoc, layerUpdates, out layer);
		}

		private static bool TryGetLayer(string key, RhinoDoc rhinoDoc, Dictionary<string, string> layerUpdates,
			out Layer layer)
		{
			if (layerUpdates.TryGetValue(key, out var oldFullPath))
			{
				var layerIndex = rhinoDoc.Layers.FindByFullPath(oldFullPath, -1);
				layer = rhinoDoc.Layers.FindIndex(layerIndex);
				return layer is not null;
			}

			layer = default;
			return false;
		}

		internal static void UpdateLayer(Layer layer, Dictionary<string, string> values, string userName)
		{
			foreach (var kvp in values)
			{
				if (!s_gettersAndSetters.TryGetValue(GetNeutralKey(kvp.Key, userName), out var setter))
				{
					continue;
				}

				setter.Set(layer, kvp.Value);
			}
		}

		private static bool IsDifferent(Func<Layer, string> getter, Layer oldState, Layer newState, out string oldValue,
			out string newValue)
		{
			oldValue = getter(oldState);
			newValue = getter(newState);

			if (!string.IsNullOrEmpty(oldValue))
			{
				return !oldValue.Equals(newValue);
			}

			return !string.IsNullOrEmpty(newValue);
		}

		public static Layer MoveLayerToExpectedPath(RhinoDoc rhinoDoc, Dictionary<string, string> layerUpdates,
			string userName)
		{
			TryGetAtOldPath(rhinoDoc, layerUpdates, userName, out var originalLayer);
			var expectedPath = layerUpdates[GetNewKey(nameof(Layer.FullPath), userName)];

			var lineage = expectedPath.Split(new[] { Separater }, StringSplitOptions.RemoveEmptyEntries).ToList();

			Layer previousLayer = null;
			for (var i = 0; i <= lineage.Count; i++)
			{
				var l = lineage.GetRange(0, i);
				var layer = string.Join(Separater, l);
				if (string.IsNullOrEmpty(layer))
				{
					continue;
				}

				var layerIndex = rhinoDoc.Layers.FindByFullPath(layer, -1);
				if (layerIndex == -1)
				{
					var newLayer = new Layer();
					if (i == lineage.Count)
					{
						newLayer = originalLayer;
						layerIndex = newLayer.Index;
					}

					var parentLayerFullName = previousLayer.FullPath;
					var parentIndex = rhinoDoc.Layers.FindByFullPath(parentLayerFullName, -1);
					newLayer.ParentLayerId = rhinoDoc.Layers.FindIndex(parentIndex)?.Id ?? Guid.Empty;

					if (!newLayer.HasIndex)
					{
						layerIndex = rhinoDoc.Layers.Add(newLayer);
					}
				}

				previousLayer = rhinoDoc.Layers.FindIndex(layerIndex);
			}

			TryGetAtExpectedPath(rhinoDoc, layerUpdates, userName, out var expectedLayer);
			return expectedLayer;
		}

		private record struct GetterAndSetter(Func<Layer, string> Get, Action<Layer, string> Set);
	}
}
