//-----------------------------------------------------------------------
// <copyright file="DeepZoomViewer.cs" company="Microsoft Corporation copyright 2008.">
// (c) 2008 Microsoft Corporation. All rights reserved.
// This source is subject to the Microsoft Public License.
// See http://www.microsoft.com/resources/sharedsource/licensingbasics/sharedsourcelicenses.mspx.
// </copyright>
// <date>02-Mar-2009</date>
// <author>Martin Grayson</author>
// <summary>A control for displaying and navigating DeepZoom content.</summary>
//-----------------------------------------------------------------------
namespace Blacklight.Controls
{
    using System;
    using System.Net;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Ink;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using System.Windows.Shapes;

    /// <summary>
    /// A control for displaying and navigating DeepZoom content.
    /// </summary>
    public class DeepZoomViewer : Control
    {
        /// <summary>
        /// The Source Dependency Property.
        /// </summary>
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(MultiScaleTileSource), typeof(DeepZoomViewer), new PropertyMetadata(new PropertyChangedCallback(Source_Changed)));

        /// <summary>
        /// The Source Dependency Property.
        /// </summary>
        public static readonly DependencyProperty SourceUriProperty =
            DependencyProperty.Register("SourceUri", typeof(Uri), typeof(DeepZoomViewer), new PropertyMetadata(SourceUri_Changed));

        /// <summary>
        /// The MultiScaleImage Template Part Name.
        /// </summary>
        private const string ElementMultiScaleImage = "MultiScaleImage";

        /// <summary>
        /// Store the multi scale image.
        /// </summary>
        private MultiScaleImage multiScaleImage;

        /// <summary>
        /// Stores if the mouse is down.
        /// </summary>
        private bool isMouseDown;
        
        /// <summary>
        /// Stores if the image is dragging.
        /// </summary>
        private bool isDragging;

        /// <summary>
        /// Stores the drag offset.
        /// </summary>
        private Point dragOffset;

        /// <summary>
        /// Stores the current position.
        /// </summary>
        private Point currentPosition;

        /// <summary>
        /// Stores the last mouse position.
        /// </summary>
        private Point lastMousePos;

        /// <summary>
        /// Stores when the control was last clicked.
        /// </summary>
        private DateTime lastClicked = DateTime.Now;

        /// <summary>
        /// Stores the current zoom level.
        /// </summary>
        private double zoomLevel = 1.0;

        /// <summary>
        /// Stores the maximum zoom level.
        /// </summary>
        private double zoomMax = 40.0;

        /// <summary>
        /// Stores the minimum zoom level.
        /// </summary>
        private double zoomMin = 0.8;

        /// <summary>
        /// Stores the home width.
        /// </summary>
        private double defaultWidth = 1;

        /// <summary>
        /// Stores the current sub image under the mouse.
        /// </summary>
        private MultiScaleSubImage currentSubImageUnderMouse;

        /// <summary>
        /// Stores the focused sub image.
        /// </summary>
        private MultiScaleSubImage focusedSubImage;

        /// <summary>
        /// DeepZoomViewer constructor.
        /// </summary>
        public DeepZoomViewer()
        {
            this.DefaultStyleKey = typeof(DeepZoomViewer);
        }

        /// <summary>
        /// Gets or sets the image source.
        /// </summary>
        /// <value>The image source.</value>
        [System.ComponentModel.Category("Deep Zoom Properties"), System.ComponentModel.Description("Gets or sets the image source.")]
        public MultiScaleTileSource Source
        {
            get { return (MultiScaleTileSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        /// <summary>
        /// Gets or sets the image source Uri.
        /// </summary>
        /// <value>The image source Uri.</value>
        [System.ComponentModel.Category("Deep Zoom Properties"), System.ComponentModel.Description("Gets or sets the image source Uri.")]
        public Uri SourceUri
        {
            get { return (Uri)GetValue(SourceUriProperty); }
            set { SetValue(SourceUriProperty, value); }
        }        

        /// <summary>
        /// Gets the parts from the template.
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            this.multiScaleImage = this.GetTemplateChild(DeepZoomViewer.ElementMultiScaleImage) as MultiScaleImage;
            if (this.multiScaleImage != null)
            {
                this.multiScaleImage.ImageOpenSucceeded += new RoutedEventHandler(this.MultiScaleImage_ImageOpenSucceeded);
                this.multiScaleImage.ImageOpenFailed += new EventHandler<ExceptionRoutedEventArgs>(this.MultiScaleImage_ImageOpenFailed);
            }
        }

        /// <summary>
        /// Updates the image source.
        /// </summary>
        /// <param name="dependencyObject">The deep zoom viewer.</param>
        /// <param name="eventArgs">Dependency Property Changed Event Args.</param>
        private static void SourceUri_Changed(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
        {
            DeepZoomViewer viewer = (DeepZoomViewer)dependencyObject;
            viewer.Source = new DeepZoomImageTileSource((Uri)eventArgs.NewValue);
        }

        /// <summary>
        /// Clears up the event handlers.
        /// </summary>
        /// <param name="dependencyObject">The deep zoom viewer.</param>
        /// <param name="eventArgs">Dependency Property Changed Event Args.</param>
        private static void Source_Changed(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
        {
            DeepZoomViewer viewer = (DeepZoomViewer)dependencyObject;
            viewer.ClearHandlers();
        }

        /// <summary>
        /// Zooms with the mouse wheel.
        /// </summary>
        /// <param name="sender">The Deep Zoom Viewer.</param>
        /// <param name="e">Mouse Wheel Event Args.</param>
        private static void MouseWheelMoved(object sender, MouseWheelEventArgs e)
        {
            DeepZoomViewer viewer = (DeepZoomViewer)sender;
            double zoomFactor = 1.5;
            if (e.Delta < 0)
            {
                zoomFactor = 0.5;
            }

            viewer.Zoom(zoomFactor, viewer.lastMousePos);
        }

        #region MultiscaleImage helper functions
        /// <summary>
        /// Gets the logical rect around a sub image.
        /// </summary>
        /// <param name="subImage">The sub image to get the rect for.</param>
        /// <returns>A rect around the sub image.</returns>
        private static Rect GetLogicalSubImageRect(MultiScaleSubImage subImage)
        {
            return new Rect(subImage.ViewportOrigin.X / -subImage.ViewportWidth, subImage.ViewportOrigin.Y / -subImage.ViewportWidth, 1.0 / subImage.ViewportWidth, 1.0 / (subImage.ViewportWidth * subImage.AspectRatio));
        }
        #endregion

        /// <summary>
        /// Moves the image about a point.
        /// </summary>
        /// <param name="focalPoint">The point to move to.</param>
        private void Move(Point focalPoint)
        {
            this.focusedSubImage = null;
            Point newOrigin = new Point();
            newOrigin.X = this.currentPosition.X - (((focalPoint.X - this.dragOffset.X) / this.multiScaleImage.ActualWidth) * this.multiScaleImage.ViewportWidth);
            newOrigin.Y = this.currentPosition.Y - (((focalPoint.Y - this.dragOffset.Y) / this.multiScaleImage.ActualHeight) * this.multiScaleImage.ViewportWidth);
            this.multiScaleImage.ViewportOrigin = newOrigin;
        }

        /// <summary>
        /// Zooms in around a point.
        /// </summary>
        /// <param name="zoomFactor">The level to zoom.</param>
        /// <param name="mousePosition">The mouse position in the image.</param>
        private void Zoom(double zoomFactor, Point mousePosition)
        {
            this.focusedSubImage = null;
            double newZoomLevel = Math.Min(this.zoomMax, Math.Max(this.zoomMin, this.zoomLevel * zoomFactor));
            double newZoomFactor = newZoomLevel / this.zoomLevel;
            this.zoomLevel = newZoomLevel;
            Point point = this.multiScaleImage.ElementToLogicalPoint(mousePosition);
            this.multiScaleImage.ZoomAboutLogicalPoint(newZoomFactor, point.X, point.Y);
        }

        /// <summary>
        /// Sets up the image event.
        /// </summary>
        /// <param name="sender">The MutliScaleImage.</param>
        /// <param name="e">Routed Event Args.</param>
        private void MultiScaleImage_ImageOpenSucceeded(object sender, RoutedEventArgs e)
        {
            this.MouseLeftButtonDown += new MouseButtonEventHandler(this.DeepZoomViewer_MouseLeftButtonDown);
            this.MouseLeftButtonUp += new MouseButtonEventHandler(this.DeepZoomViewer_MouseLeftButtonUp);
            this.MouseMove += new MouseEventHandler(this.DeepZoomViewer_MouseMove);
            MouseWheelGenerator.MouseWheelEvent.RegisterClassHandler(typeof(DeepZoomViewer), DeepZoomViewer.MouseWheelMoved, false);
        }

        /// <summary>
        /// Handles an image open failure.
        /// </summary>
        /// <param name="sender">The multi scale image.</param>
        /// <param name="e">Exception Routed Event Args.</param>
        private void MultiScaleImage_ImageOpenFailed(object sender, ExceptionRoutedEventArgs e)
        {
        }

        /// <summary>
        /// Moves the deep zoom image if the mouse is down.
        /// </summary>
        /// <param name="sender">The Deep Zoom viewer.</param>
        /// <param name="e">Mouse Event Args.</param>
        private void DeepZoomViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (this.isMouseDown && (Math.Abs(e.GetPosition(this.multiScaleImage).X - this.lastMousePos.X) > 5 || Math.Abs(e.GetPosition(this.multiScaleImage).Y - this.lastMousePos.Y) > 5))
            {
                this.isDragging = true;
            }

            this.lastMousePos = e.GetPosition(this.multiScaleImage);

            Point logicalPosition = this.multiScaleImage.ElementToLogicalPoint(this.lastMousePos);
            this.currentSubImageUnderMouse = null;

            foreach (MultiScaleSubImage image in this.multiScaleImage.SubImages)
            {
                if (GetLogicalSubImageRect(image).Contains(logicalPosition))
                {
                    this.currentSubImageUnderMouse = image;
                }
            }

            if (this.isDragging)
            {
                this.Move(this.lastMousePos);
            }
        }

        /// <summary>
        /// Handles the mouse down.
        /// </summary>
        /// <param name="sender">The Deep Zoom viewer.</param>
        /// <param name="e">Mouse Button Event Args.</param>
        private void DeepZoomViewer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ((FrameworkElement)sender).ReleaseMouseCapture();
            this.isMouseDown = false;

            if ((DateTime.Now - this.lastClicked).TotalMilliseconds > 300)
            {
                if (!this.isDragging)
                {
                    if (this.currentSubImageUnderMouse != null && this.currentSubImageUnderMouse != this.focusedSubImage)
                    {
                        this.DisplayFocalPoint(this.currentSubImageUnderMouse, new Rect(0, 0, 1, 1));
                    }
                    else
                    {
                        this.GoHome();
                    }
                }
            }

            this.isDragging = false;
            this.lastClicked = DateTime.Now;
        }

        /// <summary>
        /// Handles the mouse up.
        /// </summary>
        /// <param name="sender">The Deep Zoom viewer.</param>
        /// <param name="e">Mouse Button Event Args.</param>
        private void DeepZoomViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ((FrameworkElement)sender).CaptureMouse();
            this.isMouseDown = true;

            this.dragOffset = e.GetPosition(this);
            this.currentPosition = this.multiScaleImage.ViewportOrigin;
        }

        /// <summary>
        /// Clears the controls event handlers.
        /// </summary>
        private void ClearHandlers()
        {
            this.MouseLeftButtonDown -= new MouseButtonEventHandler(this.DeepZoomViewer_MouseLeftButtonDown);
            this.MouseLeftButtonUp -= new MouseButtonEventHandler(this.DeepZoomViewer_MouseLeftButtonUp);
            this.MouseMove -= new MouseEventHandler(this.DeepZoomViewer_MouseMove);
        }

        #region MultiScaleImage navigation functions
        /// <summary>
        /// Returns the control to the home view.
        /// </summary>
        private void GoHome()
        {
            this.focusedSubImage = null;
            this.multiScaleImage.ViewportWidth = this.defaultWidth;
            this.multiScaleImage.ViewportOrigin = new Point(0, 0);
        }

        /// <summary>
        /// Displays a focal point.
        /// </summary>
        /// <param name="subImage">The source subimage.</param>
        /// <param name="focalPoint">The focal point rect.</param>
        private void DisplayFocalPoint(MultiScaleSubImage subImage, Rect focalPoint)
        {
            this.focusedSubImage = subImage;

            Rect subImageLogicalRect = GetLogicalSubImageRect(subImage);
            Rect focalPointDisplayRect = new Rect(
                subImageLogicalRect.X + (subImageLogicalRect.Width * focalPoint.X),
                subImageLogicalRect.Y + (subImageLogicalRect.Height * focalPoint.Y),
                subImageLogicalRect.Width * focalPoint.Width,
                subImageLogicalRect.Height * focalPoint.Height);

            double width = focalPointDisplayRect.Width;
            Point origin = new Point(focalPointDisplayRect.Left, focalPointDisplayRect.Top);
            double focalPointAspectRatio = focalPointDisplayRect.Width / focalPointDisplayRect.Height;

            double num2 = this.multiScaleImage.ActualWidth / this.multiScaleImage.ActualHeight;
            if (num2 > focalPointAspectRatio)
            {
                width = (num2 / focalPointAspectRatio) * focalPointDisplayRect.Width;
                origin.X += (focalPointDisplayRect.Width - width) / 2.0;
            }
            else
            {
                double num3 = (focalPointAspectRatio / num2) * focalPointDisplayRect.Height;
                origin.Y += (focalPointDisplayRect.Height - num3) / 2.0;
            }

            this.CalculateAspectRation(1.3, ref width, ref origin);

            focalPointDisplayRect.X = origin.X;
            focalPointDisplayRect.Y = origin.Y;
            focalPointDisplayRect.Width = width;

            this.multiScaleImage.ViewportOrigin = new Point(focalPointDisplayRect.Left, focalPointDisplayRect.Top);
            this.multiScaleImage.ViewportWidth = focalPointDisplayRect.Width;

            this.zoomLevel = this.defaultWidth / focalPointDisplayRect.Width;
        }

        /// <summary>
        /// Calculates the zoom values based on the aspect ratio.
        /// </summary>
        /// <param name="scale">The desired scale.</param>
        /// <param name="width">The desired width.</param>
        /// <param name="origin">The origin point.</param>
        private void CalculateAspectRation(double scale, ref double width, ref Point origin)
        {
            double ratio = this.multiScaleImage.ActualWidth / this.multiScaleImage.ActualHeight;
            double newWidthRatio = (width * scale) - width;
            width += newWidthRatio;
            origin.X -= newWidthRatio / 2.0;
            origin.Y -= newWidthRatio / (2.0 * ratio);
        }
        #endregion
    }
}
