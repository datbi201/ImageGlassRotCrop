/*
ImageGlass - A Fast, Seamless Photo Viewer
Copyright (C) 2010 - 2027 DUONG DIEU PHAP
Project homepage: https://imageglass.org

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using ImageGlass.Common;
using ImageGlass.Common.Extensions;
using ImageGlass.Common.Types;
using System;
using System.Collections.Generic;

namespace ImageGlass.UI.Viewer;


public partial class ViewerControl
{
    // selection
    private SelectionInfo _selection = new();


    /// <summary>
    /// Occurs when the <see cref="ClientSelection"/> or <see cref="SelectionAngle"/> is changed.
    /// </summary>
    public event TEventHandler<ViewerControl, ViewerSelectionChangedEventArgs>? SelectionChanged;



    #region Public Properties

    /// <summary>
    /// Gets the upright client selection area (Photoshop-style fixed crop window).
    /// </summary>
    public Rect ClientSelection => RectSourceToClient(_selection.SourceRect);


    /// <summary>
    /// Gets, sets the image source selection. This will emit the event <see cref="SelectionChanged"/>.
    /// Use <see cref="SetSourceSelection"/> to control the selection event.
    /// </summary>
    public Rect SourceSelection
    {
        get => _selection.SourceRect;
        set => SetSourceSelection(value, true);
    }


    /// <summary>
    /// Gets, sets the clockwise rotation angle (in degrees, [-180, 180]) of the image behind the upright crop frame.
    /// </summary>
    public double SelectionAngle
    {
        get => _selection.Angle;
        set => SetSelectionAngle(value, true);
    }


    /// <summary>
    /// Gets the fixed source-pixel pivot point around which the image rotates during cropping.
    /// </summary>
    public Point SelectionRotationPivot => _selection.RotationPivotSource ?? _selection.SourceRect.Center;


    /// <summary>
    /// Gets, sets selection aspect ratio.
    /// If Width or Height is <c>less than or equals 0</c>, we will use free aspect ratio.
    /// </summary>
    public Size SelectionAspectRatio { get; set; } = new();


    /// <summary>
    /// Enables or disables the selection.
    /// </summary>
    public bool EnableSelection
    {
        get => _selection.Enabled;
        set
        {
            _selection.Enabled = value;
            if (!_selection.Enabled && Parent != null)
            {
                Cursor = Avalonia.Input.Cursor.Default;
            }
        }
    }


    /// <summary>
    /// Gets the current action of selection.
    /// </summary>
    public SelectionAction CurrentSelectionAction { get; private set; } = SelectionAction.None;


    /// <summary>
    /// Gets 8 resizers plus 4 Photoshop-style outer-corner rotation zones of the selection rectangle.
    /// </summary>
    public List<SelectionResizer> SelectionResizers
    {
        get
        {
            if (SourceSelection.IsEmpty) return [];

            var resizerSize = DpiScale(12f);
            var hitSize = DpiScale(16f);
            var halfHit = hitSize / 2;
            var rotateZone = DpiScale(28f);

            // 4 corner resize handles
            var topLeft = new Rect(ClientSelection.X, ClientSelection.Y, resizerSize, resizerSize);
            var topLeftHit = new Rect(ClientSelection.X - halfHit, ClientSelection.Y - halfHit, hitSize, hitSize);

            var topRight = new Rect(ClientSelection.Right - resizerSize, ClientSelection.Y, resizerSize, resizerSize);
            var topRightHit = new Rect(ClientSelection.Right - halfHit, ClientSelection.Y - halfHit, hitSize, hitSize);

            var bottomLeft = new Rect(ClientSelection.X, ClientSelection.Bottom - resizerSize, resizerSize, resizerSize);
            var bottomLeftHit = new Rect(ClientSelection.X - halfHit, ClientSelection.Bottom - halfHit, hitSize, hitSize);

            var bottomRight = new Rect(ClientSelection.Right - resizerSize, ClientSelection.Bottom - resizerSize, resizerSize, resizerSize);
            var bottomRightHit = new Rect(ClientSelection.Right - halfHit, ClientSelection.Bottom - halfHit, hitSize, hitSize);

            // 4 edge resize handles
            var top = new Rect(ClientSelection.X + ClientSelection.Width / 2 - resizerSize / 2, ClientSelection.Y, resizerSize, resizerSize);
            var topHit = new Rect(topLeftHit.Right, ClientSelection.Y - halfHit, Math.Max(0, topRightHit.X - topLeftHit.Right), hitSize);

            var right = new Rect(ClientSelection.Right - resizerSize, ClientSelection.Y + ClientSelection.Height / 2 - resizerSize / 2, resizerSize, resizerSize);
            var rightHit = new Rect(ClientSelection.Right - halfHit, topRightHit.Bottom, hitSize, Math.Max(0, bottomRightHit.Y - topRightHit.Bottom));

            var bottom = new Rect(ClientSelection.X + ClientSelection.Width / 2 - resizerSize / 2, ClientSelection.Bottom - resizerSize, resizerSize, resizerSize);
            var bottomHit = new Rect(bottomLeftHit.Right, ClientSelection.Bottom - halfHit, Math.Max(0, bottomRightHit.X - bottomLeftHit.Right), hitSize);

            var left = new Rect(ClientSelection.X, ClientSelection.Y + ClientSelection.Height / 2 - resizerSize / 2, resizerSize, resizerSize);
            var leftHit = new Rect(ClientSelection.X - halfHit, topLeftHit.Bottom, hitSize, Math.Max(0, bottomLeftHit.Y - topLeftHit.Bottom));

            // 4 Photoshop-style outer-corner rotation hit zones (outside each corner, after resize handles in priority)
            var rotTL = new Rect(ClientSelection.Left - rotateZone, ClientSelection.Top - rotateZone, rotateZone, rotateZone);
            var rotTR = new Rect(ClientSelection.Right, ClientSelection.Top - rotateZone, rotateZone, rotateZone);
            var rotBR = new Rect(ClientSelection.Right, ClientSelection.Bottom, rotateZone, rotateZone);
            var rotBL = new Rect(ClientSelection.Left - rotateZone, ClientSelection.Bottom, rotateZone, rotateZone);

            return [
                // 4 corners (highest priority)
                new(SelectionResizerType.BottomRight, bottomRight, bottomRightHit),
                new(SelectionResizerType.TopLeft, topLeft, topLeftHit),
                new(SelectionResizerType.BottomLeft, bottomLeft, bottomLeftHit),
                new(SelectionResizerType.TopRight, topRight, topRightHit),

                // 4 edges
                new(SelectionResizerType.Right, right, rightHit),
                new(SelectionResizerType.Bottom, bottom, bottomHit),
                new(SelectionResizerType.Left, left, leftHit),
                new(SelectionResizerType.Top, top, topHit),

                // 4 outer-corner rotation zones (Photoshop UX)
                new(SelectionResizerType.Rotate, rotTL, rotTL),
                new(SelectionResizerType.Rotate, rotTR, rotTR),
                new(SelectionResizerType.Rotate, rotBR, rotBR),
                new(SelectionResizerType.Rotate, rotBL, rotBL),
            ];
        }
    }


    #endregion // Public Properties



    #region Public Functions

    /// <summary>
    /// Sets selection area on the source image coordinates.
    /// </summary>
    public void SetSourceSelection(Rect srcRect, bool triggerEvent = true)
    {
        var w = (int)BitmapSize.Width;
        var h = (int)BitmapSize.Height;

        if (srcRect.Width <= 0 || srcRect.Height <= 0 || w <= 0 || h <= 0)
        {
            _selection.SourceRect = default;
            _selection.Angle = 0;
            _selection.RotationPivotSource = null;
        }
        else if (Math.Abs(_selection.Angle) < 1e-4)
        {
            _selection.SourceRect = srcRect.GetIntersection(new Rect(0, 0, w, h));
        }
        else
        {
            // When the image is rotated behind the crop box, allow the upright crop frame
            // to reach rotated corners smoothly without shrinking unexpectedly.
            var norm = srcRect.Normalize();
            var rw = Math.Max(1, Math.Round(norm.Width));
            var rh = Math.Max(1, Math.Round(norm.Height));
            _selection.SourceRect = new Rect(
                Math.Round(norm.X),
                Math.Round(norm.Y),
                rw,
                rh);
        }

        if (triggerEvent)
        {
            SelectionChanged?.Invoke(this, new ViewerSelectionChangedEventArgs(ClientSelection, SourceSelection, SelectionAngle));
        }
    }


    /// <summary>
    /// Sets the clockwise rotation angle (in degrees) of the image behind the upright crop frame.
    /// </summary>
    public void SetSelectionAngle(double angleDegrees, bool triggerEvent = true)
    {
        var normalized = NormalizeAngle(angleDegrees);
        if (Math.Abs(normalized) < 1e-4)
        {
            normalized = 0;
            _selection.RotationPivotSource = null;
        }
        else if (_selection.RotationPivotSource is null && !SourceSelection.IsEmpty)
        {
            // Lock pivot to the crop center once rotation starts so subsequent resizes never shift the image
            _selection.RotationPivotSource = _selection.SourceRect.Center;
        }

        _selection.Angle = normalized;

        if (triggerEvent)
        {
            SelectionChanged?.Invoke(this, new ViewerSelectionChangedEventArgs(ClientSelection, SourceSelection, SelectionAngle));
        }
    }


    #region Coordinate converters

    /// <summary>
    /// Computes the location of the client point into image source coords.
    /// </summary>
    public Point PointClientToSource(Point clientPoint)
    {
        var dpiFactor = _zooming.Factor / Dpi;
        var x = (clientPoint.X - DestRect.X) / dpiFactor + SrcRect.X;
        var y = (clientPoint.Y - DestRect.Y) / dpiFactor + SrcRect.Y;

        return new Point(x, y);
    }


    /// <summary>
    /// Computes and scale the rectangle of the client to image source coords
    /// </summary>
    public Rect RectClientToSource(Rect rect)
    {
        var safeRect = rect.Normalize();
        var p1 = PointClientToSource(new Point(safeRect.X, safeRect.Y));
        var p2 = PointClientToSource(new Point(safeRect.Right, safeRect.Bottom));


        // get the min int value
        var floorP1X = Math.Floor(Math.Round(p1.X, 1));
        var floorP1Y = Math.Floor(Math.Round(p1.Y, 1));

        if (floorP1X < 0) floorP1X = 0;
        if (floorP1Y < 0) floorP1Y = 0;
        if (floorP1X > BitmapSize.Width) floorP1X = BitmapSize.Width;
        if (floorP1Y > BitmapSize.Height) floorP1Y = BitmapSize.Height;

        if (p1 == p2) return new Rect(floorP1X, floorP1Y, 0, 0);


        // get the max int value
        var ceilP2X = Math.Ceiling(Math.Round(p2.X, 1));
        var ceilP2Y = Math.Ceiling(Math.Round(p2.Y, 1));
        if (ceilP2X < 0) ceilP2X = 0;
        if (ceilP2Y < 0) ceilP2Y = 0;
        if (ceilP2X > BitmapSize.Width) ceilP2X = BitmapSize.Width;
        if (ceilP2Y > BitmapSize.Height) ceilP2Y = BitmapSize.Height;


        var width = Math.Max(0, ceilP2X - floorP1X);
        var height = Math.Max(0, ceilP2Y - floorP1Y);

        // the selection area is where the p1 and p2 intersected.
        return new Rect(floorP1X, floorP1Y, width, height);
    }


    /// <summary>
    /// Computes the location of the image source point into client coords.
    /// </summary>
    public Point PointSourceToClient(Point srcPoint)
    {
        var dpiFactor = _zooming.Factor / Dpi;
        var x = (srcPoint.X - SrcRect.X) * dpiFactor + DestRect.X;
        var y = (srcPoint.Y - SrcRect.Y) * dpiFactor + DestRect.Y;

        return new Point(x, y);
    }


    /// <summary>
    /// Computes and scale the rectangle of the image source to client coords
    /// </summary>
    public Rect RectSourceToClient(Rect rect)
    {
        var safeRect = rect.Normalize();
        var dpiFactor = _zooming.Factor / Dpi;

        var loc = PointSourceToClient(new Point(safeRect.X, safeRect.Y));
        var size = new Size(safeRect.Width * dpiFactor, safeRect.Height * dpiFactor);

        return new Rect(loc, size);
    }

    #endregion // Coordinate converters


    #endregion // Public Functions





    #region Private Functions

    /// <summary>
    /// Normalizes an angle in degrees into the range [-180, 180].
    /// </summary>
    private static double NormalizeAngle(double angleDegrees)
    {
        var a = ((angleDegrees + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
        return a == -180.0 && angleDegrees > 0 ? 180.0 : a;
    }


    /// <summary>
    /// Initializes the selection logics when <see cref="OnPointerPressed"/> event occurs.
    /// </summary>
    private bool OnSelectionBegin(PointerPoint p)
    {
        if (_imgSource?.Image?.IsDisposed() ?? true) return false;

        _selection.PointerDownPoint = p.Position;
        _selection.IsLeftButtonPressed = p.Properties.IsLeftButtonPressed;


        // check if we can start the selection
        var canSelect = EnableSelection && p.Properties.IsLeftButtonPressed;
        var isSelectionHovered = ClientSelection.Contains(p.Position);
        var requestRerender = canSelect && !SourceSelection.IsEmpty;


        // get the selected resizer (8 resize handles first, then 4 outer-corner rotate zones)
        _selection.SelectedResizer = SelectionResizers.Find(i => i.HitRegion.Contains(p.Position));
        CurrentSelectionAction = SelectionAction.None;


        if (canSelect)
        {
            _selection.SourceRectBeforeMoved = new Rect(_selection.SourceRect.Position, _selection.SourceRect.Size);

            // rotate image behind upright crop box (Photoshop style)
            if (_selection.SelectedResizer?.Type == SelectionResizerType.Rotate)
            {
                CurrentSelectionAction = SelectionAction.Rotating;
                if (_selection.RotationPivotSource is null)
                {
                    _selection.RotationPivotSource = _selection.SourceRect.Center;
                }
                _selection.AngleBeforeRotated = _selection.Angle;
                var pivotClient = PointSourceToClient(SelectionRotationPivot);
                _selection.PointerDownAngle = Math.Atan2(p.Position.Y - pivotClient.Y, p.Position.X - pivotClient.X) * 180.0 / Math.PI;
            }
            // resize selection
            else if (_selection.SelectedResizer != null)
            {
                CurrentSelectionAction = SelectionAction.Resizing;
            }
            // move selection
            else if (isSelectionHovered)
            {
                CurrentSelectionAction = SelectionAction.Moving;
            }
            // draw new selection (resets rotation)
            else
            {
                CurrentSelectionAction = SelectionAction.Drawing;
                _selection.Angle = 0;
                _selection.RotationPivotSource = null;
            }
        }


        return requestRerender;
    }


    /// <summary>
    /// Updates selection logics when <see cref="OnPointerMoved"/> event occurs.
    /// </summary>
    private bool OnSelectionUpdating(Point position)
    {
        var requestRerender = false;
        _selection.PointerMovePoint = position;


        if (_selection.IsLeftButtonPressed)
        {
            // rotate image behind upright crop box
            if (CurrentSelectionAction == SelectionAction.Rotating)
            {
                RotateSelection(position);
                requestRerender = true;
            }
            // resize the selection
            else if (CurrentSelectionAction == SelectionAction.Resizing && _selection.SelectedResizer != null)
            {
                ResizeSelection(position, _selection.SelectedResizer.Type);
                requestRerender = true;
            }
            // move selection
            else if (CurrentSelectionAction == SelectionAction.Moving)
            {
                MoveSelection(position);
                requestRerender = true;
            }
            // draw new selection
            else if (CurrentSelectionAction == SelectionAction.Drawing)
            {
                CreateNewSelection();
                requestRerender = true;
            }
        }


        // change cursor
        if (EnableSelection)
        {
            var hoveredResizer = SelectionResizers.Find(i => i.HitRegion.Contains(position));

            if (CurrentSelectionAction == SelectionAction.Rotating || hoveredResizer?.Type == SelectionResizerType.Rotate)
            {
                Cursor = new Cursor(StandardCursorType.Hand);
                requestRerender = true; // keep curved rotate arrow following pointer smoothly
            }
            else if (hoveredResizer != null)
            {
                Cursor = hoveredResizer.GetCursor();
            }
            else if (ClientSelection.Contains(position))
            {
                Cursor = new Cursor(StandardCursorType.SizeAll);
            }
            else
            {
                Cursor = new Cursor(StandardCursorType.Cross);
            }


            // redraw the canvas
            var isSelectionHovered = ClientSelection.Contains(position);
            requestRerender = requestRerender
                || _selection.IsHovered != isSelectionHovered
                || _selection.HoveredResizer?.Type != hoveredResizer?.Type;


            _selection.IsHovered = isSelectionHovered;
            _selection.HoveredResizer = hoveredResizer;
        }
        // restore default cursor
        else
        {
            Cursor = Avalonia.Input.Cursor.Default;
        }


        return requestRerender;
    }


    /// <summary>
    /// Updates selection when one of these events occurs:
    /// <list type="bullet">
    ///   <item><see cref="OnPointerReleased"/></item>
    ///   <item><see cref="OnPointerCaptureLost"/></item>
    ///   <item><see cref="OnPointerExited"/></item>
    /// </list>
    /// </summary>
    private bool OnSelectionEnd(bool isPointerExited)
    {
        var requestRerender = false;

        if (isPointerExited)
        {
            _selection.IsHovered = false;
            _selection.HoveredResizer = null;
            _selection.PointerMovePoint = null;
            return true;
        }

        var canSelect = EnableSelection && _selection.IsLeftButtonPressed;

        _selection.PointerDownPoint = null;
        _selection.IsLeftButtonPressed = false;
        _selection.SelectedResizer = null;
        CurrentSelectionAction = SelectionAction.None;


        if (canSelect)
        {
            SelectionChanged?.Invoke(this, new ViewerSelectionChangedEventArgs(ClientSelection, SourceSelection, SelectionAngle));
            requestRerender = true;
        }

        return requestRerender;
    }


    /// <summary>
    /// Draws the Photoshop-style upright crop frame: dimmed exterior mask, adaptive alignment grid
    /// (9x9 straightening grid while rotating, 3x3 rule-of-thirds while resizing/moving),
    /// crisp L-shaped corner brackets, edge handle bars, curved corner rotate arrow, and floating pill tooltip.
    /// </summary>
    private void OnDrawSelection(DrawingContext g)
    {
        if (!EnableSelection || SourceSelection.IsEmpty) return;

        var sel = ClientSelection;
        var isRotating = CurrentSelectionAction == SelectionAction.Rotating;
        var isHoveringRotate = _selection.HoveredResizer?.Type == SelectionResizerType.Rotate;
        var hasRotation = Math.Abs(_selection.Angle) >= 1e-4;

        // 1. Dimmed exterior mask covering entire DrawingArea outside the upright crop box
        var geoOuter = new RectangleGeometry(DrawingArea);
        var geoInner = new RectangleGeometry(sel);
        var maskGeo = Geometry.Combine(geoOuter, geoInner, GeometryCombineMode.Exclude);
        var maskAlpha = (byte)(isRotating ? 155 : (_selection.IsLeftButtonPressed ? 130 : 175));
        g.DrawGeometryEx(maskGeo, Colors.Transparent, Colors.Black.WithAlpha(maskAlpha));

        // 2. Adaptive alignment grid inside the upright crop frame
        if (isRotating)
        {
            // Fine 9x9 straightening grid (Photoshop straighten overlay)
            DrawGrid(g, sel, 9, Colors.White.WithAlpha(115), Colors.Black.WithAlpha(70), 0.7f);
            // Slightly stronger 3x3 major guides
            DrawGrid(g, sel, 3, Colors.White.WithAlpha(185), Colors.Black.WithAlpha(90), 0.9f);
        }
        else if (_selection.IsLeftButtonPressed || _selection.IsHovered || _selection.HoveredResizer != null)
        {
            // Classic 3x3 rule-of-thirds grid
            DrawGrid(g, sel, 3, Colors.White.WithAlpha(175), Colors.Black.WithAlpha(110), 0.8f);
        }

        // 3. Crisp 1px selection border (dark shadow + clean white edge)
        g.DrawRectangleEx(sel, 0, Colors.Black.WithAlpha(160), null, 2.0f);
        g.DrawRectangleEx(sel, 0, Colors.White.WithAlpha(240), null, 1.0f);

        // 4. Photoshop-style L-shaped corner brackets & edge midpoint bars
        DrawPhotoshopHandles(g, sel);

        // 5. Curved double-headed rotate arrow when hovering outside any of the 4 corners or actively rotating
        if ((isHoveringRotate || isRotating) && _selection.PointerMovePoint is { } ptr)
        {
            DrawCurvedRotateArrow(g, ptr, sel.Center);
        }

        // 6. Floating dark pill tooltip (Angle while rotating; W x H while resizing/drawing/moving)
        if (isRotating)
        {
            var angleText = $"Angle: {_selection.Angle:0.0}°";
            var anchor = _selection.PointerMovePoint is { } p
                ? new Point(p.X + DpiScale(18f), p.Y + DpiScale(18f))
                : new Point(sel.Right - DpiScale(90f), sel.Bottom + DpiScale(10f));
            DrawFloatingPill(g, angleText, anchor);
        }
        else if (_selection.IsLeftButtonPressed || _selection.IsHovered || hasRotation)
        {
            var sizeText = hasRotation
                ? $"{(int)Math.Round(_selection.SourceRect.Width)} × {(int)Math.Round(_selection.SourceRect.Height)} px  ({_selection.Angle:0.0}°)"
                : $"{(int)Math.Round(_selection.SourceRect.Width)} × {(int)Math.Round(_selection.SourceRect.Height)} px";
            var pillAnchor = new Point(sel.Center.X, sel.Bottom + DpiScale(10f));
            DrawFloatingPill(g, sizeText, pillAnchor, centerHorizontally: true);
        }
    }


    /// <summary>
    /// Draws an N x N grid inside <paramref name="rect"/> with a subtle dark shadow for readability on light charts.
    /// </summary>
    private static void DrawGrid(DrawingContext g, Rect rect, int divisions, Color lineColor, Color shadowColor, float width)
    {
        var stepX = rect.Width / divisions;
        var stepY = rect.Height / divisions;

        for (int i = 1; i < divisions; i++)
        {
            var x = rect.X + i * stepX;
            var y = rect.Y + i * stepY;

            g.DrawLineEx(x, rect.Y, x, rect.Bottom, shadowColor, width + 0.8f);
            g.DrawLineEx(x, rect.Y, x, rect.Bottom, lineColor, width);

            g.DrawLineEx(rect.X, y, rect.Right, y, shadowColor, width + 0.8f);
            g.DrawLineEx(rect.X, y, rect.Right, y, lineColor, width);
        }
    }


    /// <summary>
    /// Draws crisp Photoshop-style L-shaped corner brackets and midpoint edge bars.
    /// </summary>
    private void DrawPhotoshopHandles(DrawingContext g, Rect sel)
    {
        var arm = Math.Min(DpiScale(16f), Math.Min(sel.Width, sel.Height) / 3.0);
        var thick = DpiScale(3.2f);
        var edgeBar = Math.Min(DpiScale(18f), Math.Min(sel.Width, sel.Height) / 3.5);
        var hovered = _selection.HoveredResizer?.Type;

        Color HandleColor(SelectionResizerType t) =>
            hovered == t ? Core.AccentColor : Colors.White;

        void DrawBar(Rect r, SelectionResizerType t)
        {
            var shadow = r.Inflate(0.8);
            g.DrawRectangleEx(shadow, 1f, Colors.Transparent, Colors.Black.WithAlpha(185), 0f);
            g.DrawRectangleEx(r, 0.8f, Colors.Transparent, HandleColor(t), 0f);
        }

        // Top-Left L bracket
        DrawBar(new Rect(sel.Left - thick / 2, sel.Top - thick / 2, arm, thick), SelectionResizerType.TopLeft);
        DrawBar(new Rect(sel.Left - thick / 2, sel.Top - thick / 2, thick, arm), SelectionResizerType.TopLeft);

        // Top-Right L bracket
        DrawBar(new Rect(sel.Right - arm + thick / 2, sel.Top - thick / 2, arm, thick), SelectionResizerType.TopRight);
        DrawBar(new Rect(sel.Right - thick / 2, sel.Top - thick / 2, thick, arm), SelectionResizerType.TopRight);

        // Bottom-Left L bracket
        DrawBar(new Rect(sel.Left - thick / 2, sel.Bottom - thick / 2, arm, thick), SelectionResizerType.BottomLeft);
        DrawBar(new Rect(sel.Left - thick / 2, sel.Bottom - arm + thick / 2, thick, arm), SelectionResizerType.BottomLeft);

        // Bottom-Right L bracket
        DrawBar(new Rect(sel.Right - arm + thick / 2, sel.Bottom - thick / 2, arm, thick), SelectionResizerType.BottomRight);
        DrawBar(new Rect(sel.Right - thick / 2, sel.Bottom - arm + thick / 2, thick, arm), SelectionResizerType.BottomRight);

        // Edge midpoint bars (only when crop box is large enough)
        if (sel.Width > arm * 3.2)
        {
            DrawBar(new Rect(sel.Center.X - edgeBar / 2, sel.Top - thick / 2, edgeBar, thick), SelectionResizerType.Top);
            DrawBar(new Rect(sel.Center.X - edgeBar / 2, sel.Bottom - thick / 2, edgeBar, thick), SelectionResizerType.Bottom);
        }

        if (sel.Height > arm * 3.2)
        {
            DrawBar(new Rect(sel.Left - thick / 2, sel.Center.Y - edgeBar / 2, thick, edgeBar), SelectionResizerType.Left);
            DrawBar(new Rect(sel.Right - thick / 2, sel.Center.Y - edgeBar / 2, thick, edgeBar), SelectionResizerType.Right);
        }
    }


    /// <summary>
    /// Draws a sleek Photoshop-style curved double-headed rotation arrow near the pointer,
    /// oriented tangentially to the crop center.
    /// </summary>
    private void DrawCurvedRotateArrow(DrawingContext g, Point pointer, Point center)
    {
        var radius = DpiScale(11f);
        var angleRad = Math.Atan2(pointer.Y - center.Y, pointer.X - center.X);

        // Build a curved double-headed arrow arc centered at (0, 0), spanning -48° to +48°
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            var startRad = -0.82;
            var endRad = 0.82;
            var pStart = new Point(Math.Cos(startRad) * radius, Math.Sin(startRad) * radius);
            var pEnd = new Point(Math.Cos(endRad) * radius, Math.Sin(endRad) * radius);

            ctx.BeginFigure(pStart, false);
            ctx.ArcTo(pEnd, new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            ctx.EndFigure(false);

            // Top arrowhead at pStart
            var ah = DpiScale(4.5f);
            ctx.BeginFigure(new Point(pStart.X - ah * 0.2, pStart.Y - ah * 0.9), false);
            ctx.LineTo(pStart);
            ctx.LineTo(new Point(pStart.X + ah * 0.95, pStart.Y + ah * 0.15));
            ctx.EndFigure(false);

            // Bottom arrowhead at pEnd
            ctx.BeginFigure(new Point(pEnd.X - ah * 0.2, pEnd.Y + ah * 0.9), false);
            ctx.LineTo(pEnd);
            ctx.LineTo(new Point(pEnd.X + ah * 0.95, pEnd.Y - ah * 0.15));
            ctx.EndFigure(false);
        }

        // Offset slightly outward from cursor so it never obscures the pointer tip
        var offsetDist = DpiScale(14f);
        var drawPos = new Point(
            pointer.X + Math.Cos(angleRad) * offsetDist,
            pointer.Y + Math.Sin(angleRad) * offsetDist);

        var transform = Matrix.CreateRotation(angleRad) * Matrix.CreateTranslation(drawPos.X, drawPos.Y);
        using (g.PushTransform(transform))
        {
            g.DrawGeometry(null, new Pen(new SolidColorBrush(Colors.Black.WithAlpha(210)), DpiScale(3.6f), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geo);
            g.DrawGeometry(null, new Pen(new SolidColorBrush(Colors.White), DpiScale(1.8f), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geo);
        }
    }


    /// <summary>
    /// Draws a compact dark floating pill badge (matching Photoshop's live Angle / W×H readout).
    /// </summary>
    private void DrawFloatingPill(DrawingContext g, string text, Point anchor, bool centerHorizontally = false)
    {
        var fontSize = FontSize * 0.9;
        var textSize = g.MeasureTextEx(text, FontFamily, fontSize);
        var padX = DpiScale(8f);
        var padY = DpiScale(4f);
        var w = textSize.Width + padX * 2;
        var h = textSize.Height + padY * 2;

        var x = centerHorizontally ? anchor.X - w / 2 : anchor.X;
        var y = anchor.Y;

        // Keep pill inside DrawingArea
        x = Math.Clamp(x, DrawingArea.Left + 6, Math.Max(DrawingArea.Left + 6, DrawingArea.Right - w - 6));
        if (y + h + 6 > DrawingArea.Bottom)
        {
            y = ClientSelection.Bottom - h - DpiScale(10f);
        }
        y = Math.Clamp(y, DrawingArea.Top + 6, Math.Max(DrawingArea.Top + 6, DrawingArea.Bottom - h - 6));

        var bgRect = new Rect(x, y, w, h);
        g.DrawRectangleEx(bgRect, (float)(h / 3.5), Colors.White.WithAlpha(45), Color.FromArgb(215, 24, 24, 28), 1f);
        g.DrawTextEx(text, FontFamily, fontSize, x + padX, y + padY, Colors.White);
    }



    /// <summary>
    /// Creates a new selection based on mouse pointer positions.
    /// </summary>
    private void CreateNewSelection()
    {
        if (_selection.PointerDownPoint == null || _selection.PointerMovePoint == null) return;

        var cliRect = GetRectBetween2Points(
            _selection.PointerDownPoint.Value,
            _selection.PointerMovePoint.Value,
            SelectionAspectRatio,
            BitmapSize.Width, BitmapSize.Height, DestRect);

        // limit the selected area to the image
        cliRect = cliRect.GetIntersection(DestRect);

        var srcRect = RectClientToSource(cliRect);
        SetSourceSelection(srcRect, true);
    }


    /// <summary>
    /// Rotates the image behind the upright crop box based on pointer movement around the pivot.
    /// </summary>
    private void RotateSelection(Point clientPoint)
    {
        if (!EnableSelection || SourceSelection.IsEmpty) return;

        var pivotClient = PointSourceToClient(SelectionRotationPivot);
        var currentAngle = Math.Atan2(clientPoint.Y - pivotClient.Y, clientPoint.X - pivotClient.X) * 180.0 / Math.PI;
        var delta = currentAngle - _selection.PointerDownAngle;
        var newAngle = NormalizeAngle(_selection.AngleBeforeRotated + delta);

        // Gentle snap near 0°, ±90°, ±180° for quick leveling
        const double snapThreshold = 0.6;
        foreach (var target in new[] { 0.0, 90.0, -90.0, 180.0, -180.0 })
        {
            if (Math.Abs(newAngle - target) <= snapThreshold)
            {
                newAngle = target == -180.0 ? 180.0 : target;
                break;
            }
        }

        SetSelectionAngle(Math.Round(newAngle, 1), true);
    }


    /// <summary>
    /// Gets the rectangle between 2 points with aspect ratio.
    /// </summary>
    private static Rect GetRectBetween2Points(Point? point1, Point? point2,
        Size aspectRatio, double srcWidth, double srcHeight, Rect limitRect)
    {
        var selectedArea = new Rect();
        var fromPoint = point1 ?? new Point();
        var toPoint = point2 ?? new Point();

        if (toPoint.X < fromPoint.X)
        {
            var tempX = fromPoint.X;
            fromPoint = fromPoint.WithX(toPoint.X);
            toPoint = toPoint.WithX(tempX);
        }
        if (toPoint.Y < fromPoint.Y)
        {
            var tempY = fromPoint.Y;
            fromPoint = fromPoint.WithY(toPoint.Y);
            toPoint = toPoint.WithY(tempY);
        }

        var width = Math.Abs(fromPoint.X - toPoint.X);
        var height = Math.Abs(fromPoint.Y - toPoint.Y);

        selectedArea = selectedArea.WithX(fromPoint.X);
        selectedArea = selectedArea.WithY(fromPoint.Y);
        selectedArea = selectedArea.WithWidth(width);
        selectedArea = selectedArea.WithHeight(height);

        selectedArea = selectedArea.GetIntersection(limitRect);

        if (aspectRatio.Width <= 0 || aspectRatio.Height <= 0)
            return selectedArea;

        var wRatio = aspectRatio.Width / aspectRatio.Height;
        var hRatio = aspectRatio.Height / aspectRatio.Width;

        if (wRatio > hRatio)
        {
            selectedArea = selectedArea.WithHeight(selectedArea.Width / wRatio);

            if (selectedArea.Bottom >= limitRect.Bottom)
            {
                var maxHeight = limitRect.Bottom - selectedArea.Y;
                selectedArea = selectedArea.WithWidth(maxHeight * wRatio);
                selectedArea = selectedArea.WithHeight(maxHeight);
            }
        }
        else
        {
            selectedArea = selectedArea.WithWidth(selectedArea.Height / hRatio);

            if (selectedArea.Right >= limitRect.Right)
            {
                var maxWidth = limitRect.Right - selectedArea.X;
                selectedArea = selectedArea.WithWidth(maxWidth);
                selectedArea = selectedArea.WithHeight(maxWidth * hRatio);
            }
        }

        return selectedArea;
    }


    /// <summary>
    /// Moves the upright crop frame over the (possibly rotated) image without shifting the image.
    /// </summary>
    private void MoveSelection(Point clientPoint)
    {
        if (!EnableSelection || _selection.PointerDownPoint == null) return;

        var srcPoint = PointClientToSource(clientPoint);
        var srcMouseDownPoint = PointClientToSource(_selection.PointerDownPoint.Value);

        var dX = srcMouseDownPoint.X - _selection.SourceRectBeforeMoved.X;
        var dY = srcMouseDownPoint.Y - _selection.SourceRectBeforeMoved.Y;
        var newSrcPoint = new Point(srcPoint.X - dX, srcPoint.Y - dY);

        if (Math.Abs(_selection.Angle) < 1e-4)
        {
            if (newSrcPoint.X < 0) newSrcPoint = newSrcPoint.WithX(0);
            if (newSrcPoint.Y < 0) newSrcPoint = newSrcPoint.WithY(0);
            if (newSrcPoint.X + _selection.SourceRectBeforeMoved.Width > BitmapSize.Width)
            {
                newSrcPoint = newSrcPoint.WithX(BitmapSize.Width - _selection.SourceRectBeforeMoved.Width);
            }
            if (newSrcPoint.Y + _selection.SourceRectBeforeMoved.Height > BitmapSize.Height)
            {
                newSrcPoint = newSrcPoint.WithY(BitmapSize.Height - _selection.SourceRectBeforeMoved.Height);
            }
        }

        var srcRect = new Rect(
            (int)Math.Round(newSrcPoint.X), (int)Math.Round(newSrcPoint.Y),
            (int)_selection.SourceRectBeforeMoved.Width, (int)_selection.SourceRectBeforeMoved.Height);

        SetSourceSelection(srcRect, true);
    }


    /// <summary>
    /// Resizes the upright crop frame smoothly while keeping the rotated image fixed behind it.
    /// </summary>
    private void ResizeSelection(Point clientPoint, SelectionResizerType direction)
    {
        if (!EnableSelection || _selection.PointerDownPoint == null) return;

        var srcPoint = PointClientToSource(clientPoint);
        var srcMouseDownPoint = PointClientToSource(_selection.PointerDownPoint.Value);
        var srcSelectionBeforeMoved = _selection.SourceRectBeforeMoved;
        var hasRotation = Math.Abs(_selection.Angle) >= 1e-4;
        var finalSrcRect = new Rect();

        var newX = SourceSelection.X;
        var newY = SourceSelection.Y;
        var newWidth = SourceSelection.Width;
        var newHeight = SourceSelection.Height;

        var isTopDirections = direction is SelectionResizerType.Top or SelectionResizerType.TopLeft or SelectionResizerType.TopRight;
        var isBottomDirections = direction is SelectionResizerType.Bottom or SelectionResizerType.BottomLeft or SelectionResizerType.BottomRight;
        var isLeftDirections = direction is SelectionResizerType.Left or SelectionResizerType.TopLeft or SelectionResizerType.BottomLeft;
        var isRightDirections = direction is SelectionResizerType.Right or SelectionResizerType.TopRight or SelectionResizerType.BottomRight;

        if (isTopDirections)
        {
            var gapY = srcSelectionBeforeMoved.Y - srcMouseDownPoint.Y;
            var dH = srcPoint.Y - srcSelectionBeforeMoved.Y + gapY;
            newY = srcSelectionBeforeMoved.Y + dH;
            newHeight = Math.Max(1, srcSelectionBeforeMoved.Height - dH);
        }

        if (isRightDirections)
        {
            var gapX = srcSelectionBeforeMoved.Right - srcMouseDownPoint.X;
            var dW = srcPoint.X - srcSelectionBeforeMoved.Right + gapX;
            newWidth = Math.Max(1, srcSelectionBeforeMoved.Width + dW);
        }

        if (isBottomDirections)
        {
            var gapY = srcSelectionBeforeMoved.Bottom - srcMouseDownPoint.Y;
            var dH = srcPoint.Y - srcSelectionBeforeMoved.Bottom + gapY;
            newHeight = Math.Max(1, srcSelectionBeforeMoved.Height + dH);
        }

        if (isLeftDirections)
        {
            var gapX = srcSelectionBeforeMoved.X - srcMouseDownPoint.X;
            var dW = srcPoint.X - srcSelectionBeforeMoved.X + gapX;
            newX = srcSelectionBeforeMoved.X + dW;
            newWidth = Math.Max(1, srcSelectionBeforeMoved.Width - dW);
        }

        var newSrcRect = hasRotation
            ? new Rect(newX, newY, newWidth, newHeight)
            : new Rect(newX, newY, newWidth, newHeight).GetIntersection(new Rect(0, 0, BitmapSize.Width, BitmapSize.Height));

        // Aspect ratio constraint
        if (SelectionAspectRatio.Width > 0 && SelectionAspectRatio.Height > 0)
        {
            var wRatio = SelectionAspectRatio.Width / SelectionAspectRatio.Height;
            var hRatio = SelectionAspectRatio.Height / SelectionAspectRatio.Width;

            if (wRatio > hRatio)
            {
                if (isTopDirections || isBottomDirections)
                {
                    newSrcRect = newSrcRect.WithWidth(newSrcRect.Height / hRatio);
                    if (!hasRotation && newSrcRect.Right >= BitmapSize.Width)
                    {
                        var maxWidth = BitmapSize.Width - newSrcRect.X;
                        newSrcRect = newSrcRect.WithWidth(maxWidth).WithHeight(maxWidth * hRatio);
                    }
                }
                else
                {
                    newSrcRect = newSrcRect.WithHeight(newSrcRect.Width / wRatio);
                }

                if (!hasRotation && newSrcRect.Bottom >= BitmapSize.Height)
                {
                    var maxHeight = BitmapSize.Height - newSrcRect.Y;
                    newSrcRect = newSrcRect.WithWidth(maxHeight * wRatio).WithHeight(maxHeight);
                }
            }
            else
            {
                if (isLeftDirections || isRightDirections)
                {
                    newSrcRect = newSrcRect.WithHeight(newSrcRect.Width / wRatio);
                    if (!hasRotation && newSrcRect.Bottom >= BitmapSize.Height)
                    {
                        var maxHeight = BitmapSize.Height - newSrcRect.Y;
                        newSrcRect = newSrcRect.WithWidth(maxHeight * wRatio).WithHeight(maxHeight);
                    }
                }
                else
                {
                    newSrcRect = newSrcRect.WithWidth(newSrcRect.Height / hRatio);
                }

                if (!hasRotation && newSrcRect.Right >= BitmapSize.Width)
                {
                    var maxWidth = BitmapSize.Width - newSrcRect.X;
                    newSrcRect = newSrcRect.WithWidth(maxWidth).WithHeight(maxWidth * hRatio);
                }
            }
        }

        if (isTopDirections || isLeftDirections)
        {
            finalSrcRect = new Rect(
                (int)Math.Floor(newSrcRect.X), (int)Math.Floor(newSrcRect.Y),
                (int)Math.Ceiling(newSrcRect.Width),
                (int)Math.Ceiling(newSrcRect.Height));
        }
        else
        {
            finalSrcRect = new Rect(
                (int)Math.Round(newSrcRect.X), (int)Math.Round(newSrcRect.Y),
                (int)Math.Round(newSrcRect.Width),
                (int)Math.Round(newSrcRect.Height));
        }

        if (finalSrcRect.Width <= 1) finalSrcRect = finalSrcRect.WithWidth(1);
        if (finalSrcRect.Height <= 1) finalSrcRect = finalSrcRect.WithHeight(1);

        if (direction is SelectionResizerType.Top or SelectionResizerType.TopLeft or SelectionResizerType.Left)
        {
            if (finalSrcRect.Width <= 1) finalSrcRect = finalSrcRect.WithX((int)srcSelectionBeforeMoved.Right - 1);
            if (finalSrcRect.Height <= 1) finalSrcRect = finalSrcRect.WithY((int)srcSelectionBeforeMoved.Bottom - 1);
        }
        else if (direction is SelectionResizerType.Right or SelectionResizerType.BottomRight or SelectionResizerType.Bottom)
        {
            if (finalSrcRect.Width <= 1) finalSrcRect = finalSrcRect.WithX((int)srcSelectionBeforeMoved.X);
            if (finalSrcRect.Height <= 1) finalSrcRect = finalSrcRect.WithY((int)srcSelectionBeforeMoved.Y);
        }
        else if (direction == SelectionResizerType.TopRight)
        {
            if ((finalSrcRect.Width <= 1 && finalSrcRect.Height <= 1) || (finalSrcRect.Width > 1 && finalSrcRect.Height <= 1))
            {
                finalSrcRect = finalSrcRect.WithX((int)srcSelectionBeforeMoved.Left).WithY((int)srcSelectionBeforeMoved.Bottom - 1);
            }
            else if (finalSrcRect.Width <= 1 && finalSrcRect.Height > 1)
            {
                finalSrcRect = finalSrcRect.WithX((int)srcSelectionBeforeMoved.Left);
            }
        }
        else
        {
            if (finalSrcRect.Width <= 1) finalSrcRect = finalSrcRect.WithX((int)srcSelectionBeforeMoved.Right - 1);
        }

        SetSourceSelection(finalSrcRect, true);
    }

    #endregion // Private Functions


}
