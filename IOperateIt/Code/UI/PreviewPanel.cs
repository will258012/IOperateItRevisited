// <copyright file="PreviewPanel.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace IOperateIt.UI
{
    using ColossalFramework.UI;
    using UnityEngine;

    /// <summary>
    /// Panel that contains the building preview image.
    /// </summary>
    public class PreviewPanel : UIPanel
    {
        // Panel components.
        private readonly UITextureSprite _previewSprite;
        private readonly UISprite _noPreviewSprite;
        private readonly UISprite _thumbnailSprite;
        private readonly PreviewRenderer _renderer;

        // Currently selected prefab.
        private VehicleInfo _renderInfo;

        // Currently selected color.
        private Color _color;

        // Whether to use the color value.
        private bool _useColor;

        /// <summary>
        /// Initializes a new instance of the <see cref="PreviewPanel"/> class.
        /// </summary>
        internal PreviewPanel()
        {
            // Size.
            width = 370f;
            height = 350f;

            // Appearance.
            opacity = 1.0f;

            _previewSprite = AddUIComponent<UITextureSprite>();
            _previewSprite.size = size;
            _previewSprite.relativePosition = Vector2.zero;

            _noPreviewSprite = AddUIComponent<UISprite>();
            _noPreviewSprite.size = size;
            _noPreviewSprite.relativePosition = Vector2.zero;

            _thumbnailSprite = AddUIComponent<UISprite>();
            _thumbnailSprite.size = size;
            _thumbnailSprite.relativePosition = Vector2.zero;

            // Initialise renderer; use double size for anti-aliasing.
            _renderer = gameObject.AddComponent<PreviewRenderer>();
            _renderer.size = _previewSprite.size * 2;

            // Click-and-drag rotation.
            eventMouseDown += (component, _) => eventMouseMove += RotateCamera;

            eventMouseUp += (component, _) => eventMouseMove -= RotateCamera;

            // Zoom with mouse wheel.
            eventMouseWheel += (_, mouseEvent) =>
            {
                _renderer.zoom -= Mathf.Sign(mouseEvent.wheelDelta) * 0.25f;

                // Render updated image.
                RenderPreview();
            };
        }

        /// <summary>
        /// Sets the prefab to render.
        /// </summary>
        /// <param name="info">Prefab to render.</param>
        public void SetTarget(VehicleInfo info, Color color = default, bool useColor = false)
        {
            // Update current selection to the new prefab.
            _renderInfo = info;

            // Update current selection color.
            _color = color;

            // Update color state.
            _useColor = useColor;

            // Show the updated render.
            RenderPreview();
        }

        /// <summary>
        /// Render and show a preview of a vehicle.
        /// </summary>
        public void RenderPreview()
        {
            bool validRender = false;

            // Don't render anything without a mesh or material.
            if (_renderInfo?.m_mesh != null && _renderInfo.m_material != null)
            {
                // Render.
                _renderer.RenderVehicle(_renderInfo, _color, _useColor);

                // We got a valid render; ensure preview sprite is square (decal previews can change width), set display texture, and set status flag.
                _previewSprite.relativePosition = Vector2.zero;
                _previewSprite.size = size;
                _previewSprite.texture = _renderer.texture;
                validRender = true;
            }

            // If not a valid render, try to show thumbnail instead.
            else if (_renderInfo.m_Atlas != null && !string.IsNullOrEmpty(_renderInfo.m_Thumbnail))
            {
                // Show thumbnail.
                ShowThumbnail(_renderInfo.m_Atlas, _renderInfo.m_Thumbnail);

                // All done here.
                return;
            }

            // Reset background if we didn't get a valid render.
            if (!validRender)
            {
                _previewSprite.Hide();
                _thumbnailSprite.Hide();
                _noPreviewSprite.Show();
                return;
            }

            // If we got here, we should have a render; show it.
            _noPreviewSprite.Hide();
            _thumbnailSprite.Hide();
            _previewSprite.Show();
        }

        /// <summary>
        /// Rotates the preview camera (model rotation) in accordance with mouse movement.
        /// </summary>
        /// <param name="c">Calling component.</param>
        /// <param name="p">Mouse event pareameter.</param>
        private void RotateCamera(UIComponent c, UIMouseEventParameter p)
        {
            // Change rotation.
            _renderer.cameraRotation -= p.moveDelta.x / _previewSprite.width * 360f;

            // Render updated image.
            RenderPreview();
        }

        /// <summary>
        /// Displays a prefab's UI thumbnail (instead of a render or blank panel).
        /// </summary>
        /// <param name="atlas">Thumbnail atlas.</param>
        /// <param name="thumbnail">Thumbnail sprite name.</param>
        private void ShowThumbnail(UITextureAtlas atlas, string thumbnail)
        {
            // Set thumbnail.
            _thumbnailSprite.atlas = atlas;
            _thumbnailSprite.spriteName = thumbnail;

            // Show thumbnail sprite and hide others.
            _noPreviewSprite.Hide();
            _previewSprite.Hide();
            _thumbnailSprite.Show();
        }
    }
}