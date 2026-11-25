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

namespace ImageGlass.UI.Viewer;


/// <summary>
/// Holds information about selection feature.
/// </summary>
internal class SelectionInfo
{
    /// <summary>
    /// Indicates whether Selection feature is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;


    /// <summary>
    /// Represents the rectangular area of a source image (in rotated-view source coordinates).
    /// </summary>
    public Rect SourceRect { get; set; } = default;


    /// <summary>
    /// Represents the rectangle area before a move or resize operation.
    /// </summary>
    public Rect SourceRectBeforeMoved { get; set; } = default;


    /// <summary>
    /// Gets or sets the clockwise rotation angle of the image behind the upright crop frame in degrees [-180, 180].
    /// </summary>
    public double Angle { get; set; } = 0;


    /// <summary>
    /// Fixed pivot point (in source image pixels) around which the image rotates while cropping,
    /// so resizing or moving the upright crop frame never shifts the rotated image.
    /// </summary>
    public Point? RotationPivotSource { get; set; } = null;


    /// <summary>
    /// Stores the selection angle at the start of a rotation drag.
    /// </summary>
    public double AngleBeforeRotated { get; set; } = 0;


    /// <summary>
    /// Stores the pointer polar angle (degrees) relative to the pivot when rotation drag begins.
    /// </summary>
    public double PointerDownAngle { get; set; } = 0;


    /// <summary>
    /// Indicates whether the selection is currently being hovered.
    /// </summary>
    public bool IsHovered { get; set; } = false;


    /// <summary>
    /// Represents the hovered resizer in the selection area.
    /// </summary>
    public SelectionResizer? HoveredResizer { get; set; } = null;


    /// <summary>
    /// Represents the selected resizer in the selection area.
    /// </summary>
    public SelectionResizer? SelectedResizer { get; set; } = null;


    /// <summary>
    /// Represents the current position of the pointer when the left button is pressed.
    /// </summary>
    public Point? PointerDownPoint { get; set; } = null;


    /// <summary>
    /// Represents the current position of the pointer when the left button is moved.
    /// </summary>
    public Point? PointerMovePoint { get; set; } = null;


    /// <summary>
    /// Indicates whether the left button is currently pressed.
    /// </summary>
    public bool IsLeftButtonPressed { get; set; } = false;

}
