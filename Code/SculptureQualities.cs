using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.Components;
using Game.Constants;
using Game.Data;
using Game.Rendering;
using Game.Utils;
using KL.Collections;
using KL.Utils;
using UnityEngine;

namespace Sculptures.Constants {
	[Serializable]
	public static class SculptureQualities {
		public static readonly Dictionary<string, SculptureQuality> All = new Dictionary<string, SculptureQuality>();

		private static bool hasLoaded;
		private static bool hasLoadedVariations;

        private static readonly string Simple = "Simple";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Init() {
			All.Clear();
			hasLoaded = false;
			hasLoadedVariations = false;
		}

		public static void Load() {
			if (hasLoaded) {
				return;
			}
			hasLoaded = true;
			List<SculptureQuality> list = The.ModLoader.LoadObjects<SculptureQuality>("Config/SculptureQualities");
            SculptureQuality quality;
			for (int i = 0; i < list.Count; i++) {
				quality = list[i];
				if (All.PutGet(quality.Id, quality) != null) {
					D.Warn("SculptureQualities.All already has quality with same id: {0}!", quality.Id);
				}
			}
		}

		public static SculptureQuality Get(string id) {
			if (id == null) {
				D.Err("Getting sculpture quality by id null");
				id = Simple;
			}
			Load();
			SculptureQuality quality = All.Get(id, null);
			D.Ass(quality != null, "Sculpture quality Type {0} is not defined! See Config/SculptureQualities folder", id);
			return quality;
		}

		// Load sculptures types and create tile variations for qualities
		public static void LoadVariations() {
			if (hasLoadedVariations) {
				return;
			}
			List<SculptureType> types = The.ModLoader.LoadObjects<SculptureType>("Config/SculptureTypes");
			if (!types.Any()) {
				D.Err("No sculpture types found to load variations for");
				return;
			}
			D.Warn("Sculpture types: " + types.Count);
			Load();
			Dictionary<string, SculptureQuality> qualities = All;
			D.Warn("Sculpture qualities: " + qualities.Count);
			foreach (SculptureType type in types) {
				Def constructableDef = The.Defs.TryGet("Objects/Furniture/Sculpture" + type.Id);
				if (constructableDef == null) {
					D.Err("Definition for sculpture constructable type {0} could not be found!", type.Id);
					continue;
				}
				D.Warn("Load constructable variations for " + constructableDef.Id);
				string constructableGraphic = constructableDef.ComponentConfigFor("TileGraphics").GetString("Graphic");
				foreach (SculptureQuality quality in qualities.Values) {
					// Create variations for constructable sculptures
					string variationId = WithQuality(constructableDef.Id, quality);
					D.Warn("Variation id: " + variationId);
					string graphic = WithQuality(constructableGraphic, quality);
					Def variation = constructableDef.CreateVariation(variationId);
					variation.ResearchValue = 1;
					variation.IsAbstract = false;
					variation.LayerId = WorldLayer.ToId(variation.Layer);
					ComponentConfig tileConfig = variation.ComponentConfigFor("TileGraphics");
					tileConfig.SetProperty(new SerializableProperty
					{
						Key = "Graphic",
						String = graphic
					});
					tileConfig.SetProperty(new SerializableProperty
					{
						Key = "HasHeight",
						Bool = quality.HasHeight
					});
					variation.ComponentConfigFor("Aesthetics").SetProperty(new SerializableProperty
					{
						Key = "Aesthetics",
						Float = quality.Aesthetics
					});
					variation.ComponentConfigFor("Constructable").SetProperty(new SerializableProperty
					{
						Key = "Contents",
						RawMaterials = new Mat[2] {
							new Mat {
								TypeId = type.Material,
								StackSize = 1
							},
							new Mat {
								TypeId = "Steel",
								StackSize = 1
							}
						}
					});
					variation.NameKey = WithQuality(constructableDef.NameKey, quality, true);
					The.Defs.Set(variation);
				}
			}
			hasLoadedVariations = true;
		}

		private static string WithQuality(string name, SculptureQuality quality) {
			return WithQuality(name, quality, false);
		}

		private static string WithQuality(string name, SculptureQuality quality, bool lowerCase) {
			return name + "_" + (lowerCase ? quality.Id.ToLower() : quality.Id);
		}

		public static int GetVariationsCount(GameState state, Def baseDef) {
			int num = state.CountByDef(baseDef);
			if (!baseDef.HasVariations) {
				return num;
			}
			foreach (Def def in baseDef.Variations) {
				num += state.CountByDef(def);
			}
			return num;
		}
    }
}