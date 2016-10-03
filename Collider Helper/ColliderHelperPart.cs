using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using KSP.UI.Screens;
// ReSharper disable ArrangeThisQualifier
// ReSharper disable ForCanBeConvertedToForeach

namespace ColliderHelper
{
    public enum RendererState
    {
        Active,
        Symmetry,
        Off
    }

    public class ColliderHelperPart : PartModule
    {
        private RendererState _state = RendererState.Off;

        [KSPEvent(guiActive = true, guiActiveUnfocused = true, guiActiveUncommand = true, externalToEVAOnly = false,
            guiActiveEditor = true, unfocusedRange = 100f, guiName = "Show Collider: Off", active = true,
            advancedTweakable = true, isPersistent = false)]
        public void ColliderHelperEvent()
        {
            CycleState();
        }

        public void CycleState()
        {
            switch(_state)
            {
                case RendererState.Active:
                    // on->symmetry|off
                    if (this.part.symmetryCounterparts.Count > 0)
                    {
                        SetSymmetry(true);
                    }
                    else
                    {
                        SetOff(false);
                    }
                    break;
                case RendererState.Symmetry:
                    // symmetry->off
                    SetOff(true);
                    break;
                case RendererState.Off:
                    // off->on
                    if (this.part.symmetryCounterparts.Count > 0)
                    {
                        var onCount = 0;
                        for (var i = 0; i < this.part.symmetryCounterparts.Count; i++)
                        {
                            if (this.part.symmetryCounterparts[i].GetComponent<ColliderHelperPart>()._state ==
                                RendererState.Active)
                                onCount++;
                        }

                        if (onCount == this.part.symmetryCounterparts.Count)
                            SetSymmetry(true);
                        else
                            SetOn(false);
                    }
                    else
                    {
                        SetOn(false);
                    }
                    break;
            }
        }

        public void SetOn(bool symmetry)
        {
            if (this.gameObject.GetComponent<WireframeComponent>() == null)
                this.gameObject.AddComponent<WireframeComponent>();

            if (!symmetry)
            {
                _state = RendererState.Active;

                Events["ColliderHelperEvent"].guiName = "Show Collider: On";
            }
        }

        public void SetSymmetry(bool recursive)
        {
            if (recursive)
            {
                for (var i = 0; i < this.part.symmetryCounterparts.Count; i++)
                {
                    var component = this.part.symmetryCounterparts[i].GetComponent<ColliderHelperPart>();
                    if (component != null)
                        component.SetSymmetry(false);
                }
            }

            SetOn(true);

            _state = RendererState.Symmetry;

            Events["ColliderHelperEvent"].guiName = "Show Collider: Symmetry";
        }

        public void SetOff(bool recursive)
        {
            if (recursive)
            {
                for (var i = 0; i < this.part.symmetryCounterparts.Count; i++)
                {
                    var helperComponent = this.part.symmetryCounterparts[i].GetComponent<ColliderHelperPart>();
                    if (helperComponent != null)
                        helperComponent.SetOff(false);
                }
            }

            var renderComponent = this.gameObject.GetComponent<WireframeComponent>();
            if (renderComponent != null)
                Destroy(renderComponent);

            _state = RendererState.Off;

            Events["ColliderHelperEvent"].guiName = "Show Collider: Off";
        }
    }
}