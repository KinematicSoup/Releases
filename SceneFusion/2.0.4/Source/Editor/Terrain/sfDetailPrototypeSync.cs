using System.Collections.Generic;
using UnityEngine;
using KS.SF.Reactor;

namespace KS.SceneFusion2.Unity.Editor
{
    // Manages syncing of DetailPrototypes and Properties
    class sfDetailPrototypeSync
    {
        private delegate void PrototypeSetter(DetailPrototype prototype, sfBaseProperty property);
        private Dictionary<string, PrototypeSetter> m_setters = new Dictionary<string, PrototypeSetter>();

        public sfDetailPrototypeSync()
        {
            RegisterPrototypeSetters();
        }

        private void RegisterPrototypeSetters()
        {
            m_setters[sfProp.HealthyColor] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.healthyColor = property.As<Color>();
            };

            m_setters[sfProp.DryColor] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.dryColor = property.As<Color>();
            };

            m_setters[sfProp.MinWidth] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.minWidth = (float)property;
            };

            m_setters[sfProp.MaxWidth] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.maxWidth = (float)property;
            };

            m_setters[sfProp.MinHeight] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.minHeight = (float)property;
            };

            m_setters[sfProp.MaxHeight] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.maxHeight = (float)property;
            };

            m_setters[sfProp.NoiseSpread] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.noiseSpread = (float)property;
            };

            m_setters[sfProp.RenderMode] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.renderMode = (DetailRenderMode)(int)property;
            };

            m_setters[sfProp.Prototype] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.prototype = sfPropertyUtils.ToReference(property, prototype.prototype);
            };

            m_setters[sfProp.PrototypeTexture] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.prototypeTexture = sfPropertyUtils.ToReference(property, prototype.prototypeTexture);
            };

            m_setters[sfProp.UsePrototypeMesh] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.usePrototypeMesh = (bool)property;
            };
            m_setters[sfProp.NoiseSeed] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.noiseSeed = (int)property;
            };

            m_setters[sfProp.HoleEdgePadding] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.holeEdgePadding = (float)property;
            };

            m_setters[sfProp.UseInstancing] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.useInstancing = (bool)property;
            };
#if UNITY_2022_2_OR_NEWER
            m_setters[sfProp.AlignToGround] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.alignToGround = (float)property;
            };

            m_setters[sfProp.PositionJitter] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.positionJitter = (float)property;
            };

            m_setters[sfProp.UseDensityScale] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.useDensityScaling = (bool)property;
            };

            m_setters[sfProp.Density] = (DetailPrototype prototype, sfBaseProperty property) =>
            {
                prototype.density = (float)property;
            };
#endif
        }

        /// <summary>Serializes a detail prototype.</summary>
        /// <param name="prototype">prototype to serialize.</param>
        /// <param name="updated">
        /// set to true if a stand-in asset on the prototype was replaced with the correct
        /// asset.
        /// </param>
        /// <returns>the detail prototype as a dictionary property.</returns>
        public sfDictionaryProperty Serialize(DetailPrototype prototype, ref bool updated)
        {
            sfDictionaryProperty dict = new sfDictionaryProperty();
            dict[sfProp.HealthyColor] = new sfValueProperty(prototype.healthyColor);
            dict[sfProp.DryColor] = new sfValueProperty(prototype.dryColor);
            dict[sfProp.MinWidth] = new sfValueProperty(prototype.minWidth);
            dict[sfProp.MaxWidth] = new sfValueProperty(prototype.maxWidth);
            dict[sfProp.MinHeight] = new sfValueProperty(prototype.minHeight);
            dict[sfProp.MaxHeight] = new sfValueProperty(prototype.maxHeight);
            dict[sfProp.NoiseSpread] = new sfValueProperty(prototype.noiseSpread);
            dict[sfProp.RenderMode] = new sfValueProperty((int)prototype.renderMode);

            dict[sfProp.Prototype] = sfPropertyUtils.FromReference(prototype.prototype);
            GameObject newPrototype = sfLoader.Get().GetAssetForStandIn(prototype.prototype);
            if (newPrototype != null)
            {
                prototype.prototype = newPrototype;
                updated = true;
            }

            dict[sfProp.PrototypeTexture] = sfPropertyUtils.FromReference(prototype.prototypeTexture);
            Texture2D newTexture = sfLoader.Get().GetAssetForStandIn(prototype.prototypeTexture);
            if (newTexture != null)
            {
                prototype.prototypeTexture = newTexture;
                updated = true;
            }

            dict[sfProp.UsePrototypeMesh] = new sfValueProperty(prototype.usePrototypeMesh);
            dict[sfProp.NoiseSeed] = new sfValueProperty(prototype.noiseSeed);
            dict[sfProp.HoleEdgePadding] = new sfValueProperty(prototype.holeEdgePadding);
            dict[sfProp.UseInstancing] = new sfValueProperty(prototype.useInstancing);
#if UNITY_2022_2_OR_NEWER
            dict[sfProp.AlignToGround] = new sfValueProperty(prototype.alignToGround);
            dict[sfProp.PositionJitter] = new sfValueProperty(prototype.positionJitter);
            dict[sfProp.UseDensityScale] = new sfValueProperty(prototype.useDensityScaling);
            dict[sfProp.Density] = new sfValueProperty(prototype.density);
#endif
            return dict;
        }

        public DetailPrototype Deserialize(sfDictionaryProperty dict)
        {
            DetailPrototype prototype = new DetailPrototype();
            UpdatePrototype(prototype, dict);
            return prototype;
        }

        /// <summary>Update the prototype fields from a property</summary>
        public void UpdatePrototype(DetailPrototype prototype, sfBaseProperty property)
        {
            PrototypeSetter setter;
            if (property.Type == sfBaseProperty.Types.DICTIONARY)
            {
                sfDictionaryProperty dict = property as sfDictionaryProperty;
                foreach (KeyValuePair<string, sfBaseProperty> pair in dict)
                {
                    if (m_setters.TryGetValue(pair.Value.Name, out setter))
                    {
                        setter(prototype, pair.Value);
                    }
                }

            }
            else if (m_setters.TryGetValue(property.Name, out setter))
            {
                setter(prototype, property);
            }
        }

        /// <summary>Update properties from a prototype</summary>
        /// <param name="-">Detail prototype property</param>
        /// <param name="-">Prototype to check and update</param>
        /// <param name="updatedPrototype">
        /// Set to true if any stand-in assets on the prototype were replaced with
        /// the correct asset.
        /// </param>
        public bool UpdateProperties(sfDictionaryProperty dict, DetailPrototype prototype, ref bool updatedPrototype)
        {
            bool updated = false;
            updated |= UpdateProperty(dict[sfProp.HealthyColor], prototype.healthyColor);
            updated |= UpdateProperty(dict[sfProp.DryColor], prototype.dryColor);
            updated |= UpdateProperty(dict[sfProp.MinWidth], prototype.minWidth);
            updated |= UpdateProperty(dict[sfProp.MaxWidth], prototype.maxWidth);
            updated |= UpdateProperty(dict[sfProp.MinHeight], prototype.minHeight);
            updated |= UpdateProperty(dict[sfProp.MaxHeight], prototype.maxHeight);
            updated |= UpdateProperty(dict[sfProp.NoiseSpread], prototype.noiseSpread);
            updated |= UpdateProperty(dict[sfProp.RenderMode], (int)prototype.renderMode);

            updated |= sfPropertyManager.Get().Copy(dict[sfProp.Prototype], 
                sfPropertyUtils.FromReference(prototype.prototype));
            GameObject newPrototype = sfLoader.Get().GetAssetForStandIn(prototype.prototype);
            if (newPrototype != null)
            {
                prototype.prototype = newPrototype;
                updatedPrototype = true;
            }

            updated |= sfPropertyManager.Get().Copy(dict[sfProp.PrototypeTexture], 
                sfPropertyUtils.FromReference(prototype.prototypeTexture));
            Texture2D newTexture = sfLoader.Get().GetAssetForStandIn(prototype.prototypeTexture);
            if (newTexture != null)
            {
                prototype.prototypeTexture = newTexture;
                updatedPrototype = true;
            }

            updated |= UpdateProperty(dict[sfProp.UsePrototypeMesh], prototype.usePrototypeMesh);
            updated |= UpdateProperty(dict[sfProp.NoiseSeed], prototype.noiseSeed);
            updated |= UpdateProperty(dict[sfProp.HoleEdgePadding], prototype.holeEdgePadding);
            updated |= UpdateProperty(dict[sfProp.UseInstancing], prototype.useInstancing);
#if UNITY_2022_2_OR_NEWER
            updated |= UpdateProperty(dict[sfProp.AlignToGround], prototype.alignToGround);
            updated |= UpdateProperty(dict[sfProp.PositionJitter], prototype.positionJitter);
            updated |= UpdateProperty(dict[sfProp.UseDensityScale], prototype.useDensityScaling);
            updated |= UpdateProperty(dict[sfProp.Density], prototype.density);
#endif
            return updated;
        }

        public bool UpdateProperty(sfBaseProperty property, ksMultiType value)
        {
            if (property.Type == sfBaseProperty.Types.VALUE)
            {
                if (!(property as sfValueProperty).Value.Equals(value))
                {
                    (property as sfValueProperty).Value = value;
                    return true;
                }
                return false;
            }

            if (property.Type == sfBaseProperty.Types.STRING)
            {
                if ((property as sfStringProperty).String != value.String)
                {
                    (property as sfStringProperty).String = value.String;
                    return true;
                }
                return false;
            }
            return false;
        }
    }
}
        /// <returns>
        /// true if the dict property was updated.
        /// */
        /// </returns>
