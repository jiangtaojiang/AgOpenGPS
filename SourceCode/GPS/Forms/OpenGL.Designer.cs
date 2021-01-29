using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Text;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormGPS
    {
        //extracted Near, Far, Right, Left clipping planes of frustum
        public double[] frustum = new double[24];

        int mouseX = 0, mouseY = 0;
        public double offX, offY;
        private bool zoomUpdateCounter = false;


        // When oglMain is created
        private void oglMain_Load(object sender, EventArgs e)
        {
            LoadGLTextures();
        }

        //oglMain needs a resize
        private void oglMain_Resize(object sender, EventArgs e)
        {
            oglMain.MakeCurrent();
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.CullFace(CullFaceMode.Back);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Viewport(0, 0, oglMain.Width, oglMain.Height);

            Matrix4 mat = Matrix4.CreatePerspectiveFieldOfView(0.7f, oglMain.AspectRatio, 10.0f, 600000.0f);
            GL.LoadMatrix(ref mat);
            GL.MatrixMode(MatrixMode.Modelview);
        }

        int deadCam = 0;

        StringBuilder sb = new StringBuilder();
        private void oglMain_Paint(object sender, PaintEventArgs e)
        {
            oglMain.MakeCurrent();

            if (recvCounter < 134)
            {
                if (isGPSPositionInitialized)
                {
                    //  Clear the color and depth buffer.
                    GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);

                    if (isDay) GL.ClearColor(0.27f, 0.4f, 0.7f, 1);
                    else GL.ClearColor(0, 0, 0, 1);

                    GL.LoadIdentity();

                    //position the camera
                    camera.SetWorldCam(pivotAxlePos.Easting + offX, pivotAxlePos.Northing + offY, camHeading);

                    //the bounding box of the camera for cullling.
                    CalcFrustum();

                    worldGrid.DrawFieldSurface();

                    ////if grid is on draw it
                    if (isGridOn) worldGrid.DrawWorldGrid(camera.gridZoom);

                    if (isDrawPolygons) GL.PolygonMode(MaterialFace.Front, PolygonMode.Line);

                    GL.Enable(EnableCap.Blend);
                    //draw patches of sections

                    //initialize the steps for mipmap of triangles (skipping detail while zooming out)
                    int mipmap = 0;
                    if (camera.camSetDistance < -800) mipmap = 2;
                    if (camera.camSetDistance < -1500) mipmap = 4;
                    if (camera.camSetDistance < -2400) mipmap = 8;
                    if (camera.camSetDistance < -5000) mipmap = 16;

                    DrawPatchList(mipmap);
                    DrawSectionsPatchList(true);

                    GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);
                    GL.Color3(1, 1, 1);

                    //if (recPath.isRecordOn)
                    recPath.DrawRecordedLine();
                    recPath.DrawDubins();

                    //draw Boundaries
                    GL.LineWidth(1);
                    bnd.DrawBoundaryLines();

                    if (isAutoLoadFields && (mc.isOutOfBounds || !isJobStarted))
                    {
                        GL.LineWidth(2);
                        GL.Color3(0.0f, 1.0f, 0.0f);

                        int TestInsideBoundary = -1;

                        for (int i = 0; i < Fields.Count; i++)
                        {
                            if (NotLoadedField && Fields[i].Eastingmin <= pn.fix.Easting && Fields[i].Eastingmax >= pn.fix.Easting && Fields[i].Northingmin <= pn.fix.Northing && Fields[i].Northingmax >= pn.fix.Northing)
                            {
                                bool oddNodes = false;
                                int k = Fields[i].Polygon.Points.Count - 1;
                                for (int j = 0; j < Fields[i].Polygon.Points.Count; j++)
                                {
                                    if ((Fields[i].Polygon.Points[j].Northing < pn.fix.Northing && Fields[i].Polygon.Points[k].Northing >= pn.fix.Northing
                                    || Fields[i].Polygon.Points[k].Northing < pn.fix.Northing && Fields[i].Polygon.Points[j].Northing >= pn.fix.Northing)
                                    && (Fields[i].Polygon.Points[j].Easting <= pn.fix.Easting || Fields[i].Polygon.Points[k].Easting <= pn.fix.Easting))
                                    {
                                        oddNodes ^= (Fields[i].Polygon.Points[j].Easting + (pn.fix.Northing - Fields[i].Polygon.Points[j].Northing) /
                                        (Fields[i].Polygon.Points[k].Northing - Fields[i].Polygon.Points[j].Northing) * (Fields[i].Polygon.Points[k].Easting - Fields[i].Polygon.Points[j].Easting) < pn.fix.Easting);
                                    }
                                    k = j;
                                }

                                if (oddNodes && currentFieldDirectory != Fields[i].Dir)
                                {
                                    TestInsideBoundary = i;

                                    FileSaveEverythingBeforeClosingField();
                                    currentFieldDirectory = Fields[i].Dir;
                                    FileOpenField();
                                    Text = "AgOpenGPS - " + currentFieldDirectory;
                                }
                            }

                            if (i == TestInsideBoundary)
                            {
                                GL.Color3(1.0f, 0.0f, 0.0f);
                            }

                            Fields[i].Polygon.DrawPolygon(false);
                            if (i == TestInsideBoundary)
                            {
                                GL.Color3(0.0f, 1.0f, 0.0f);
                            }
                        }
                    }

                    //draw the turnLines
                    if ((Guidance.isYouTurnBtnOn || isUTurnAlwaysOn) && Guidance.CurrentEditLine < 0)
                    {
                        bnd.DrawTurnLines();
                    }

                    if (mc.isOutOfBounds) bnd.DrawGeoFenceLines();

                    if (bnd.BtnHeadLand)
                    {
                        GL.Color3(0.960f, 0.96232f, 0.30f);
                        for (int i = 0; i < bnd.Boundaries.Count; i++)
                        {
                            if (bnd.Boundaries[i].Eastingmin > worldGrid.EastingMax || bnd.Boundaries[i].Eastingmax < worldGrid.EastingMin) continue;
                            if (bnd.Boundaries[i].Northingmin > worldGrid.NorthingMax || bnd.Boundaries[i].Northingmax < worldGrid.NorthingMin) continue;
                            for (int j = 0; j < bnd.Boundaries[i].HeadLand.Count; j++)
                            {
                                bnd.Boundaries[i].HeadLand[j].DrawPolygon(false);
                            }
                        }
                    }

                    if (Guidance.BtnGuidanceOn) Guidance.DrawLine();


                    GL.Color4(0.8630f, 0.73692f, 0.60f, 0.25);
                    Guidance.DrawTram(false);

                    if (flagPts.Count > 0)
                    {
                        DrawFlags();

                        //Direct line to flag if flag selected
                        if (flagNumberPicked > 0 && flagNumberPicked < 255)
                        {
                            if (flagPts[flagNumberPicked - 1].Easting < worldGrid.EastingMax || flagPts[flagNumberPicked - 1].Easting > worldGrid.EastingMin && flagPts[flagNumberPicked - 1].Northing < worldGrid.NorthingMax || flagPts[flagNumberPicked - 1].Northing > worldGrid.NorthingMin)
                            {
                                GL.LineWidth(lineWidth);
                                GL.Enable(EnableCap.LineStipple);
                                GL.LineStipple(1, 0x0707);
                                GL.Begin(PrimitiveType.Lines);
                                GL.Color3(0.930f, 0.72f, 0.32f);
                                GL.Vertex3(pivotAxlePos.Easting, pivotAxlePos.Northing, 0);
                                GL.Vertex3(flagPts[flagNumberPicked - 1].Easting, flagPts[flagNumberPicked - 1].Northing, 0);
                                GL.End();
                                GL.Disable(EnableCap.LineStipple);
                            }
                        }
                    }

                    //Quick fix for drawing the vehicle if all boundaries are off the plane
                    GL.Begin(PrimitiveType.LineLoop);
                    GL.Vertex3(0, 0, -100000);
                    GL.Vertex3(0, 1, -100000);
                    GL.End();

                    vehicle.DrawVehicle();

                    //draw the vehicle/implement
                    for (int i = 0; i < Tools.Count; i++)
                    {
                        Tools[i].DrawTool();
                    }

                    // 2D Ortho ---------------------------------------////////-------------------------------------------------

                    GL.MatrixMode(MatrixMode.Projection);
                    GL.PushMatrix();
                    GL.LoadIdentity();

                    //negative and positive on width, 0 at top to bottom ortho view
                    GL.Ortho(-(double)oglMain.Width / 2, (double)oglMain.Width / 2, (double)oglMain.Height, 0, -1, 1);

                    //  Create the appropriate modelview matrix.
                    GL.MatrixMode(MatrixMode.Modelview);
                    GL.PushMatrix();
                    GL.LoadIdentity();

                    if (isSkyOn) DrawSky();

                    //LightBar if AB Line is set and turned on or contour
                    if (isLightbarOn)
                    {
                        DrawLightBarText();
                    }
                    DrawCompassText();

                    if ((ahrs.isRollFromAutoSteer || ahrs.isRollFromAVR || ahrs.isRollFromOGI))
                        DrawRollBar();

                    if (bnd.Boundaries.Count > 0 && Guidance.isYouTurnBtnOn) DrawUTurnBtn();

                    if (isAutoSteerBtnOn) DrawManUTurnBtn();

                    if (isCompassOn) DrawCompass(isSpeedoOn);

                    if (isSpeedoOn) DrawSpeedo();

                    GL.Disable(EnableCap.Texture2D);
                    GL.PopMatrix();

                    if (vehicle.BtnHydLiftOn) DrawLiftIndicator();

                    if (isRTK)
                    {
                        if (!(pn.FixQuality == 4 || pn.FixQuality == 5)) DrawLostRTK();
                    }

                    //if (isJobStarted) DrawFieldText();

                    GL.Flush();//finish openGL commands
                    GL.PopMatrix();//  Pop the modelview.

                    ////-------------------------------------------------ORTHO END---------------------------------------

                    //  back to the projection and pop it, then back to the model view.
                    GL.MatrixMode(MatrixMode.Projection);
                    GL.PopMatrix();
                    GL.MatrixMode(MatrixMode.Modelview);

                    //reset point size
                    GL.PointSize(1.0f);
                    GL.Flush();
                    oglMain.SwapBuffers();

                    if (LeftMouseDownOnOpenGL) MakeFlagMark();

                    //draw the zoom window
                    if (isJobStarted && zoomUpdateCounter == true && oglZoom.Width != 400)
                    {
                        zoomUpdateCounter = false;
                        oglZoom.Refresh();
                    }

                    //if a minute has elapsed save the field in case of crash and to be able to resume            
                    if (MinuteCounter > 60 && recvCounter < 134)
                    {
                        MinuteCounter = 0;
                        NMEAWatchdog.Enabled = false;

                        //save nmea log file
                        if (isLogNMEA) FileSaveNMEA();

                        //don't save if no gps
                        if (isJobStarted)
                        {
                            StartTasks(null, 7, TaskName.Save);

                            //NMEA log file
                            if (isLogElevation) FileSaveElevation();
                            //FileSaveFieldKML();
                        }

                        if (isAutoDayNight && ++TenMinuteCounter > 10)
                        {
                            TenMinuteCounter = 0;
                            isDayTime = (DateTime.Now.Ticks < sunset.Ticks && DateTime.Now.Ticks > sunrise.Ticks);

                            if (isDayTime != isDay)
                            {
                                isDay = !isDayTime;
                                SwapDayNightMode();
                            }

                            if (sunrise.Date != DateTime.Today)
                            {
                                IsBetweenSunriseSunset(Latitude, Longitude);

                                //set display accordingly
                                isDayTime = (DateTime.Now.Ticks < sunset.Ticks && DateTime.Now.Ticks > sunrise.Ticks);

                            }
                        }

                        //go see if data ready for draw and position updates
                        NMEAWatchdog.Enabled = true;
                    }
                }
            }
            else//No Valid Position
            {
                GL.Enable(EnableCap.Blend);
                GL.ClearColor(0.25122f, 0.258f, 0.275f, 1.0f);

                GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
                GL.LoadIdentity();

                SetZoom(-40);
                GL.Translate(0, 0, -20);
                //rotate the camera down to look at fix
                GL.Rotate(-60, 1, 0, 0);

                camHeading = 0;

                deadCam++;
                deadCam %= 1080;
                GL.Rotate(deadCam / 3, 0.0, 0.0, 1.0);
                ////draw the guide
                GL.Begin(PrimitiveType.Triangles);
                GL.Color3(0.98f, 0.0f, 0.0f);
                GL.Vertex3(0.0f, -1.0f, 0.0f);
                GL.Color3(0.0f, 0.98f, 0.0f);
                GL.Vertex3(-1.0f, 1.0f, 0.0f);
                GL.Color3(0.98f, 0.98f, 0.0f);
                GL.Vertex3(1.0f, -0.0f, 0.0f);
                GL.End();                       // Done Drawing Reticle

                GL.Rotate(deadCam + 90, 0.0, 0.0, 1.0);
                font.DrawText3D(0, 0, "  I'm Lost  ");
                GL.Color3(0.98f, 0.98f, 0.70f);

                GL.Rotate(deadCam + 180, 0.0, 0.0, 1.0);
                font.DrawText3D(0, 0, "   No GPS   ");


                // 2D Ortho ---------------------------------------////////-------------------------------------------------

                GL.MatrixMode(MatrixMode.Projection);
                GL.PushMatrix();
                GL.LoadIdentity();

                //negative and positive on width, 0 at top to bottom ortho view
                GL.Ortho(-(double)oglMain.Width / 2, (double)oglMain.Width / 2, (double)oglMain.Height, 0, -1, 1);


                //  Create the appropriate modelview matrix.
                GL.MatrixMode(MatrixMode.Modelview);
                GL.PushMatrix();
                GL.LoadIdentity();

                GL.Color3(0.98f, 0.98f, 0.70f);

                int edge = -oglMain.Width / 2 + 10;
                int line = 20;

                font.DrawText(edge, line, "NMEA: " + recvSentenceSettings, 1);
                line += 30;
                if (spGPS.IsOpen)
                {
                    font.DrawText(edge, line, "GPS Port: Connected", 1);
                }
                else
                {
                    font.DrawText(edge, line, "GPS Port: Not Connected", 1);
                }

                line += 30;

                if (spAutoSteer.IsOpen)
                {
                    font.DrawText(edge, line, "AutoSteer Port: Connected", 1);
                }
                else
                {
                    font.DrawText(edge, line, "AutoSteer Port: Not Connected", 1);
                }
                line += 30;
                if (spMachine.IsOpen)
                {
                    font.DrawText(edge, line, "Machine Port: Connected", 1);
                }
                else
                {
                    font.DrawText(edge, line, "Machine Port: Not Connected", 1);
                }
                line += 30;
                if (Properties.Settings.Default.setUDP_isOn)
                {
                    font.DrawText(edge, line, "UDP: Counter is " + pbarUDP.ToString(), 1);
                }
                else
                {
                    font.DrawText(edge, line, "UDP: Off", 1);
                }
                line += 30;

                GL.Flush();//finish openGL commands
                GL.PopMatrix();//  Pop the modelview.

                ////-------------------------------------------------ORTHO END---------------------------------------

                //  back to the projection and pop it, then back to the model view.
                GL.MatrixMode(MatrixMode.Projection);
                GL.PopMatrix();
                GL.MatrixMode(MatrixMode.Modelview);

                //reset point size
                GL.PointSize(1.0f);

                GL.Flush();
                oglMain.SwapBuffers();

            }
        }

        private void oglBack_Load(object sender, EventArgs e)
        {
            oglBack.MakeCurrent();
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.PixelStore(PixelStoreParameter.PackAlignment, 1);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.ClearColor(0, 0, 0, 1.0f);

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Viewport(0, 0, 750, 300);
            Matrix4 mat = Matrix4.CreatePerspectiveFieldOfView(0.06f, 2.5f, 480f, 1520f);
            GL.LoadMatrix(ref mat);
            GL.MatrixMode(MatrixMode.Modelview);
        }

        private void oglBack_Paint(object sender, PaintEventArgs e)
        {
            oglBack.MakeCurrent();

            double mOn, mOff;
            int start, end, tagged, rpHeight, rpHeight2, totalPixs;
            bool isDraw;

            CalculateSectionLookAhead();
            bnd.isToolUp = true;
            for (int i = 0; i < Tools.Count; i++)
            {
                if (Tools[i].Sections.Count > 0)
                {
                    GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
                    GL.LoadIdentity();// Reset The View

                    //back the camera up
                    GL.Translate(0, 0, -500);

                    //rotate camera so heading matched fix heading in the world
                    GL.Rotate(Glm.ToDegrees(Tools[i].ToolWheelPos.Heading), 0, 0, 1);

                    //translate to that spot in the world 
                    GL.Translate(-Tools[i].ToolHitchPos.Easting - Math.Sin(Tools[i].ToolWheelPos.Heading) * 14, -Tools[i].ToolHitchPos.Northing - Math.Cos(Tools[i].ToolWheelPos.Heading) * 14, 0);

                    if (bnd.BtnHeadLand && bnd.Boundaries.Count > 0 && bnd.Boundaries[0].HeadLand.Count > 0)
                    {
                        GL.Color3(0.0f, 0.5f, 0.0f);
                        bnd.Boundaries[0].Polygon.DrawPolygon(true);

                        GL.Color3(0.0f, 1.0f, 0.0f);
                        for (int k = 0; k < bnd.Boundaries[0].HeadLand.Count; k++)
                        {
                            bnd.Boundaries[0].HeadLand[k].DrawPolygon(true);
                        }
                    }
                    else if (bnd.Boundaries.Count > 0)
                    {
                        GL.Color3(0.0f, 1.0f, 0.0f);
                        bnd.Boundaries[0].Polygon.DrawPolygon(true);
                    }

                    for (int k = 1; k < bnd.Boundaries.Count; k++)
                    {
                        if (bnd.BtnHeadLand && bnd.Boundaries[k].HeadLand.Count > 0)
                        {
                            GL.Color3(0.0f, 0.5f, 0.0f);
                            for (int l = 0; l < bnd.Boundaries[0].HeadLand.Count; l++)
                            {
                                bnd.Boundaries[k].HeadLand[l].DrawPolygon(true);
                            }
                            GL.Color3(0.0f, 0.0f, 0.0f);
                            bnd.Boundaries[k].Polygon.DrawPolygon(true);
                        }
                        else
                        {
                            GL.Color3(0.0f, 0.0f, 0.0f);
                            bnd.Boundaries[k].Polygon.DrawPolygon(true);
                        }
                    }

                    GL.Color4(0.0f, 0.5f, 0.0f, 0.05f);
                    GL.Enable(EnableCap.Blend);

                    //for every new chunk of patch
                    for (int j = 0; j < PatchDrawList.Count; j++)
                    {
                        isDraw = false;
                        int count2 = PatchDrawList[j].Count;
                        for (int l = 1; l < count2; l += 3)
                        {
                            if (PatchDrawList[j][l].Easting > pivotAxlePos.Easting + 50)
                                continue;
                            if (PatchDrawList[j][l].Easting < pivotAxlePos.Easting - 50)
                                continue;
                            if (PatchDrawList[j][l].Northing > pivotAxlePos.Northing + 50)
                                continue;
                            if (PatchDrawList[j][l].Northing < pivotAxlePos.Northing - 50)
                                continue;
                            //point is in frustum so draw the entire patch
                            isDraw = true;
                            break;
                        }

                        if (isDraw)
                        {
                            //draw the triangles in each triangle strip
                            GL.Begin(PrimitiveType.TriangleStrip);
                            for (int l = 1; l < count2; l++) GL.Vertex3(PatchDrawList[j][l].Easting, PatchDrawList[j][l].Northing, 0);
                            GL.End();
                        }
                    }

                    DrawSectionsPatchList(false);

                    Guidance.DrawTram(true);

                    //finish it up - we need to read the ram of video card
                    GL.Flush();

                    //determine farthest ahead lookahead - is the height of the readpixel line
                    rpHeight = (int)Math.Round(Math.Max(Math.Max((bnd.BtnHeadLand && vehicle.BtnHydLiftOn ? Math.Max(vehicle.hydLiftLookAheadDistanceRight, vehicle.hydLiftLookAheadDistanceLeft) : 0), Math.Max(Tools[i].lookAheadDistanceOnPixelsRight, Tools[i].lookAheadDistanceOnPixelsLeft)), 2), MidpointRounding.AwayFromZero);
                    rpHeight2 = (int)Math.Round(Math.Max(Math.Min(Tools[i].lookAheadDistanceOffPixelsRight, Tools[i].lookAheadDistanceOffPixelsLeft), 0), MidpointRounding.AwayFromZero);

                    byte[] GreenPixels = new byte[(Tools[i].rpWidth * (rpHeight - rpHeight2))];

                    //read the whole block of pixels up to max lookahead, one read only
                    GL.ReadPixels(Tools[i].rpXPosition, 10 + rpHeight2, Tools[i].rpWidth, rpHeight - rpHeight2, OpenTK.Graphics.OpenGL.PixelFormat.Green, PixelType.UnsignedByte, GreenPixels);

                    if (DrawBackBuffer)
                    {
                        //Paint to context for troubleshooting
                        oglBack.BringToFront();
                        oglBack.SwapBuffers();
                    }
                    else oglBack.SendToBack();

                    //is applied area coming up?
                    totalPixs = 0;

                    //assume all sections are on and super can be on, if not set false to turn off.
                    bool isSuperSectionAllowedOn = true;

                    //determine if in or out of headland, do hydraulics if on
                    if (bnd.BtnHeadLand && vehicle.BtnHydLiftOn)
                    {
                        //calculate the slope
                        double m = (vehicle.hydLiftLookAheadDistanceRight - vehicle.hydLiftLookAheadDistanceLeft) / Tools[i].rpWidth;

                        totalPixs = 0;
                        for (int pos = 0; pos < Tools[i].rpWidth; pos++)
                        {
                            int height = (int)(vehicle.hydLiftLookAheadDistanceLeft + (m * pos)) + 1;
                            for (int a = pos; a < height * Tools[i].rpWidth; a += Tools[i].rpWidth)
                            {
                                if (a >= 0 && a < GreenPixels.Length && GreenPixels[a] > 127)
                                {
                                    totalPixs++;
                                    bnd.isToolUp = false;
                                    goto GetOutTool;
                                }
                            }
                        }

                        GetOutTool:

                        if (totalPixs == 0)
                        {
                            bnd.isToolUp = bnd.isToolUp & true;
                        }
                    }

                    ///////////////////////////////////////////   Section control   ///////////////////////////////////////////

                    for (int j = 0; j < Tools[i].numOfSections; j++)
                    {
                        // Manual on, force the section On and exit loop so digital is also overidden
                        if (Tools[i].Sections[j].BtnSectionState == btnStates.On && autoBtnState != 0)
                        {
                            if (j + 1 < Tools[i].numOfSections)
                            {
                                if (Tools[i].Sections[j].positionRight == Tools[i].Sections[j + 1].positionLeft)
                                {
                                    isSuperSectionAllowedOn &= true;
                                }
                                else isSuperSectionAllowedOn = false;
                            }
                            Tools[i].Sections[j].SectionOnRequest = true;
                        }
                        else if (Tools[i].Sections[j].BtnSectionState == btnStates.Off || autoBtnState == 0)
                        {
                            Tools[i].Sections[j].SectionOnRequest = false;
                            Tools[i].Sections[j].SectionOverlapTimer = 1;
                            isSuperSectionAllowedOn = false;
                        }
                        else if (Tools[i].Sections[j].BtnSectionState == btnStates.Auto)
                        {
                            double speeddif = (Tools[i].ToolFarRightSpeed - Tools[i].ToolFarLeftSpeed) / Tools[i].rpWidth;

                            start = Tools[i].Sections[j].rpSectionPosition - Tools[i].Sections[0].rpSectionPosition;
                            end = start + Tools[i].Sections[j].rpSectionWidth;
                            if ((Tools[i].ToolFarLeftSpeed + speeddif * start) * 3.6 <= Tools[i].SlowSpeedCutoff && (Tools[i].ToolFarLeftSpeed + speeddif * end) * 3.6 <= Tools[i].SlowSpeedCutoff)
                            {
                                Tools[i].Sections[j].SectionOnRequest = false;
                                Tools[i].Sections[j].SectionOverlapTimer = 1;
                                isSuperSectionAllowedOn = false;
                            }
                            else
                            {
                                int StopHeight = 1, StartHeight = 1;
                                mOn = (Tools[i].lookAheadDistanceOnPixelsRight - Tools[i].lookAheadDistanceOnPixelsLeft) / Tools[i].rpWidth;
                                mOff = (Tools[i].lookAheadDistanceOffPixelsRight - Tools[i].lookAheadDistanceOffPixelsLeft) / Tools[i].rpWidth;

                                if (end > Tools[i].rpWidth) end = Tools[i].rpWidth;

                                tagged = 0;
                                int tagged2 = 0;
                                totalPixs = 0; 

                                for (int pos = start; pos < end; pos++)
                                {
                                    StartHeight = (int)Math.Round((Tools[i].lookAheadDistanceOffPixelsLeft - rpHeight2) + (mOff * pos), MidpointRounding.AwayFromZero) * Tools[i].rpWidth + pos;
                                    StopHeight = (int)Math.Round((Tools[i].lookAheadDistanceOnPixelsLeft - rpHeight2) + (mOn * pos), MidpointRounding.AwayFromZero) * Tools[i].rpWidth + pos;

                                    for (int a = StartHeight; a <= StopHeight; a += Tools[i].rpWidth)
                                    {
                                        if (a > 0 && a < GreenPixels.Length)
                                        {
                                            totalPixs++;
                                            double tt = GreenPixels[a];

                                            if (GreenPixels[a] == 255 || (bnd.Boundaries.Count == 0 && GreenPixels[a] == 0))
                                            {
                                                ++tagged;
                                            }
                                            else if (bnd.Boundaries.Count > 0 && GreenPixels[a] < 127)
                                            {
                                                ++tagged2;
                                            }
                                        }
                                    }
                                }

                                if ((tagged2 < totalPixs*0.1) && tagged != 0 && (tagged * 100.0) / totalPixs >= Tools[i].toolMinUnappliedPixels)
                                {
                                    Tools[i].Sections[j].IsSectionRequiredOn = true;
                                }
                                else
                                {
                                    Tools[i].Sections[j].IsSectionRequiredOn = false;
                                }

                                Tools[i].Sections[j].SectionOnRequest = Tools[i].Sections[j].IsSectionRequiredOn;

                                if (j < Tools[i].numOfSections)
                                {
                                    if (j+1 == Tools[i].numOfSections || Tools[i].Sections[j].positionRight == Tools[i].Sections[j + 1].positionLeft)
                                    {
                                        isSuperSectionAllowedOn &= (Tools[i].SuperSection && Tools[i].Sections[j].SectionOnRequest) || (Tools[i].Sections[j].IsSectionOn && Tools[i].Sections[j].IsMappingOn);
                                    }
                                    else isSuperSectionAllowedOn = false;
                                }
                            }
                        }
                    }
                    Tools[i].SuperSection = isSuperSectionAllowedOn;
                }
            }

            if (bnd.BtnHeadLand && vehicle.BtnHydLiftOn)
            {
                if (bnd.isToolUp && mc.Send_HydraulicLift[5] != 0x02)
                {
                    mc.Send_HydraulicLift[5] = 0x02;
                    UpdateSendDataText("Hydraulic Lift: State Up");
                }
                else if (!bnd.isToolUp && mc.Send_HydraulicLift[5] != 0x01)
                {
                    mc.Send_HydraulicLift[5] = 0x01;
                    UpdateSendDataText("Hydraulic Lift: State Down");
                }
            }
            else if(mc.Send_HydraulicLift[5] != 0x00)
            {
                mc.Send_HydraulicLift[5] = 0x00;
                UpdateSendDataText("HydraulicLift: State Off");
            }
            SendData(mc.Send_HydraulicLift, false);

            //Determine if sections want to be on or off
            ProcessSectionOnOffRequests();
        }

        private void oglZoom_Load(object sender, EventArgs e)
        {
            oglZoom.MakeCurrent();
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.PixelStore(PixelStoreParameter.PackAlignment, 1);

            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.ClearColor(0, 0, 0, 1.0f);
        }

        private void oglZoom_Resize(object sender, EventArgs e)
        {
            oglZoom.MakeCurrent();
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            GL.Viewport(0, 0, oglZoom.Width, oglZoom.Height);
            //58 degrees view
            Matrix4 mat = Matrix4.CreatePerspectiveFieldOfView(1.01f, 1.0f, 100.0f, (float)(maxFieldDistance +10));
            GL.LoadMatrix(ref mat);

            GL.MatrixMode(MatrixMode.Modelview);
        }

        private void oglZoom_Paint(object sender, PaintEventArgs e)
        {
            if (isJobStarted)
            {
                if (oglZoom.Width != 400)
                {
                    oglZoom.MakeCurrent();

                    GL.Enable(EnableCap.Blend);

                    GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
                    GL.LoadIdentity();                  // Reset The View

                    //back the camera up
                    GL.Translate(0, 0, -maxFieldDistance);

                    //translate to that spot in the world 
                    GL.Translate(-fieldCenterX, -fieldCenterY, 0);

                    if (isDay) GL.Color3(sectionColorDay.R, sectionColorDay.G, sectionColorDay.B);
                    else GL.Color3(sectionColorDay.R, sectionColorDay.G, sectionColorDay.B);

                    CalcFrustum();

                    DrawPatchList(8);

                    if (isDay) GL.Color4(sectionColorDay.R, sectionColorDay.G, sectionColorDay.B, (byte)152);
                    else GL.Color4(sectionColorDay.R, sectionColorDay.G, sectionColorDay.B, (byte)(152 * 0.5));

                    DrawSectionsPatchList(false);
                    /*
                    if (true)
                    {
                        GL.Finish();

                        int grnHeight = oglZoom.Height;
                        int grnWidth = oglZoom.Width;
                        byte[] overPix = new byte[grnHeight * grnWidth + 1];

                        GL.ReadPixels(0, 0, grnWidth, grnWidth, OpenTK.Graphics.OpenGL.PixelFormat.Green, PixelType.UnsignedByte, overPix);

                        int once = 0;
                        int twice = 0;
                        int more = 0;
                        int level = 0;
                        double total = 0;
                        double total2 = 0;

                        //50, 96, 112
                        for (int i = 0; i < grnHeight * grnWidth; i++)
                        {
                            once++;

                            if (overPix[i] > 105)
                            {
                                more++;
                                level = overPix[i];
                            }
                            else if (overPix[i] > 85)
                            {
                                twice++;
                                level = overPix[i];
                            }
                            else if (overPix[i] > 50)
                            {
                            }
                        }
                        total = once + twice + more;
                        total2 = total + twice + more + more;

                        if (total2 > 0)
                        {
                            fd.actualAreaCovered = (total / total2 * fd.workedAreaTotal);
                            fd.overlapPercent = Math.Round(((1 - total / total2) * 100), 2);
                        }
                        else
                        {
                            fd.actualAreaCovered = fd.overlapPercent = 0;
                        }
                    }
                    */

                    //draw the ABLine
                    if (Guidance.BtnGuidanceOn)
                    {
                        int ptC = Guidance.curList.Count;
                        if (ptC > 2)
                        {
                            GL.LineWidth(2);
                            GL.Color3(0.925f, 0.2f, 0.90f);
                            GL.Begin(PrimitiveType.LineStrip);
                            for (int h = 0; h < ptC; h++) GL.Vertex3(Guidance.curList[h].Easting, Guidance.curList[h].Northing, 0);
                            GL.End();
                        }
                    }

                    //draw all the boundaries
                    bnd.DrawBoundaryLines();

                    GL.PointSize(8.0f);
                    GL.Begin(PrimitiveType.Points);
                    GL.Color3(0.95f, 0.90f, 0.0f);
                    GL.Vertex3(pivotAxlePos.Easting, pivotAxlePos.Northing, 0.0);
                    GL.End();

                    GL.PointSize(1.0f);

                    GL.Flush();
                    oglZoom.MakeCurrent();
                    oglZoom.SwapBuffers();
                }
            }
        }

        private void DrawManUTurnBtn()
        {
            GL.Enable(EnableCap.Texture2D);

            GL.BindTexture(TextureTarget.Texture2D, texture[5]);        // Select Our Texture
            GL.Color3(0.90f, 0.90f, 0.293f);

            int two3 = oglMain.Width / 4;
            GL.Begin(PrimitiveType.Quads);              // Build Quad From A Triangle Strip
            {
                GL.TexCoord2(0, 0); GL.Vertex2(-82 - two3, 45); // 
                GL.TexCoord2(1, 0); GL.Vertex2(82 - two3, 45.0); // 
                GL.TexCoord2(1, 1); GL.Vertex2(82 - two3, 120); // 
                GL.TexCoord2(0, 1); GL.Vertex2(-82 - two3, 120); //
            }
            GL.End();
            GL.Disable(EnableCap.Texture2D);

        }

        private void DrawUTurnBtn()
        {
            GL.Enable(EnableCap.Texture2D);

            if (!Guidance.isYouTurnTriggered)
            {
                GL.BindTexture(TextureTarget.Texture2D, texture[3]);        // Select Our Texture
                if (distancePivotToTurnLine > 0 && !Guidance.isOutOfBounds) GL.Color3(0.3f, 0.95f, 0.3f);
                else GL.Color3(0.97f, 0.635f, 0.4f);
            }
            else
            {
                GL.BindTexture(TextureTarget.Texture2D, texture[4]);        // Select Our Texture
                GL.Color3(0.90f, 0.90f, 0.293f);
            }

            int two3 = oglMain.Width / 5;
            GL.Begin(PrimitiveType.Quads);              // Build Quad From A Triangle Strip
            if (Guidance.isYouTurnRight)
            {
                GL.TexCoord2(0, 0); GL.Vertex2(-62 + two3, 50); // 
                GL.TexCoord2(1, 0); GL.Vertex2(62 + two3, 50.0); // 
                GL.TexCoord2(1, 1); GL.Vertex2(62 + two3, 120); // 
                GL.TexCoord2(0, 1); GL.Vertex2(-62 + two3, 120); //
            }
            else
            {
                GL.TexCoord2(1, 0); GL.Vertex2(-62 + two3, 50); // 
                GL.TexCoord2(0, 0); GL.Vertex2(62 + two3, 50.0); // 
                GL.TexCoord2(0, 1); GL.Vertex2(62 + two3, 120); // 
                GL.TexCoord2(1, 1); GL.Vertex2(-62 + two3, 120); //
            }
            //
            GL.End();
            GL.Disable(EnableCap.Texture2D);
            // Done Building Triangle Strip
            if (isMetric)
            {
                if (!Guidance.isYouTurnTriggered)
                {
                    font.DrawText(-30 + two3, 80, DistPivotM);
                }
                else 
                {
                    font.DrawText(-30 + two3, 80, ((int)(Guidance.ytLength - Guidance.onA) + " m"));
                }
            }
            else
            {

                if (!Guidance.isYouTurnTriggered)
                {
                    font.DrawText(-40 + two3, 85, DistPivotFt);
                }
                else
                {
                    font.DrawText(-40 + two3, 85, ((int)(Glm.m2ft * (Guidance.ytLength - Guidance.onA)) + " ft"));
                }
            }
        }

        private void DrawSteerCircle()
        {
            int center = oglMain.Width / 2 - 45;
            int bottomSide = oglMain.Height - 42;

            GL.PushMatrix();
            GL.Enable(EnableCap.Texture2D);

            GL.BindTexture(TextureTarget.Texture2D, texture[11]);        // Select Our Texture


            GL.Translate(center, bottomSide, 0);

            GL.Begin(PrimitiveType.Quads);              // Build Quad From A Triangle Strip
            {
                GL.TexCoord2(0, 0); GL.Vertex2(-48, -48); // 
                GL.TexCoord2(1, 0); GL.Vertex2(48, -48.0); // 
                GL.TexCoord2(1, 1); GL.Vertex2(48, 48); // 
                GL.TexCoord2(0, 1); GL.Vertex2(-48, 48); //
            }
            GL.End();
            GL.Disable(EnableCap.Texture2D);
            GL.PopMatrix();

            //string pwm;
            //if (guidanceLineDistanceOff == 32020 | guidanceLineDistanceOff == 32000)
            //{
            //    pwm = "Off";
            //}
            //else
            //{
            //    pwm = mc.pwmDisplay.ToString();
            //}
            
            //center = oglMain.Width / -2 + 38 - (int)(((double)(pwm.Length) * 0.5) * 16);
            //GL.Color3(0.7f, 0.7f, 0.53f);

            //font.DrawText(center, 65, pwm, 0.8);
        }

        private void MakeFlagMark()
        {
            LeftMouseDownOnOpenGL = false;
            byte[] data1 = new byte[768];

            //scan the center of click and a set of square points around
            GL.ReadPixels(mouseX - 8, mouseY - 8, 16, 16, PixelFormat.Rgb, PixelType.UnsignedByte, data1);

            //made it here so no flag found
            flagNumberPicked = 0;

            for (int ctr = 0; ctr < 768; ctr += 3)
            {
                if (data1[ctr] == 255 | data1[ctr + 1] == 255)
                {
                    flagNumberPicked = data1[ctr + 2];
                    break;
                }
            }

            if (flagNumberPicked > 0)
            {
                Form fc = Application.OpenForms["FormFlags"];

                if (fc != null)
                {
                    fc.Focus();
                    return;
                }

                if (flagPts.Count > 0)
                {
                    Form form = new FormFlags(this);
                    form.Show(this);
                }
            }
        }

        private void DrawFlags()
        {
            int flagCnt = flagPts.Count;
            for (int f = 0; f < flagCnt; f++)
            {
                if (flagPts[f].Easting > worldGrid.EastingMax || flagPts[f].Easting < worldGrid.EastingMin) continue;
                if (flagPts[f].Northing > worldGrid.NorthingMax || flagPts[f].Northing < worldGrid.NorthingMin) continue;

                GL.PointSize(8.0f);
                GL.Begin(PrimitiveType.Points);
                if (flagPts[f].color == 0) GL.Color3((byte)255, (byte)0, (byte)flagPts[f].ID);
                if (flagPts[f].color == 1) GL.Color3((byte)0, (byte)255, (byte)flagPts[f].ID);
                if (flagPts[f].color == 2) GL.Color3((byte)255, (byte)255, (byte)flagPts[f].ID);
                GL.Vertex3(flagPts[f].Easting, flagPts[f].Northing, 0);
                GL.End();

                font.DrawText3D(flagPts[f].Easting, flagPts[f].Northing, "&" + flagPts[f].notes);
                //else
                //    font.DrawText3D(flagPts[f].Easting, flagPts[f].Northing, "&");
            }

            if (flagNumberPicked != 0)
            {
                ////draw the box around flag
                double offSet = (camera.zoomValue * camera.zoomValue * 0.01);
                GL.LineWidth(4);
                GL.Color3(0.980f, 0.0f, 0.980f);
                GL.Begin(PrimitiveType.LineStrip);
                GL.Vertex3(flagPts[flagNumberPicked - 1].Easting, flagPts[flagNumberPicked - 1].Northing + offSet, 0);
                GL.Vertex3(flagPts[flagNumberPicked - 1].Easting - offSet, flagPts[flagNumberPicked - 1].Northing, 0);
                GL.Vertex3(flagPts[flagNumberPicked - 1].Easting, flagPts[flagNumberPicked - 1].Northing - offSet, 0);
                GL.Vertex3(flagPts[flagNumberPicked - 1].Easting + offSet, flagPts[flagNumberPicked - 1].Northing, 0);
                GL.Vertex3(flagPts[flagNumberPicked - 1].Easting, flagPts[flagNumberPicked - 1].Northing + offSet, 0);
                GL.End();

                //draw the flag with a black dot inside
                //GL.PointSize(4.0f);
                //GL.Color3(0, 0, 0);
                //GL.Begin(PrimitiveType.Points);
                //GL.Vertex3(flagPts[flagNumberPicked - 1].Easting, flagPts[flagNumberPicked - 1].Northing, 0);
                //GL.End();
            }
        }

        private void DrawLightBar(double Width, double Height, double offlineDistance)
        {
            double down = 13;
            GL.LineWidth(1);
            //GL.Translate(0, 0, 0.01);
            //offlineDistance *= -1;
            //  Dot distance is representation of how far from AB Line
            int dotDistance = (int)(offlineDistance);
            int limit = (int)lightbarCmPerPixel * 8;
            if (dotDistance < -limit) dotDistance = -limit;
            if (dotDistance > limit) dotDistance = limit;

            //if (dotDistance < -10) dotDistance -= 30;
            //if (dotDistance > 10) dotDistance += 30;

            // dot background
            GL.PointSize(8.0f);
            GL.Color3(0.00f, 0.0f, 0.0f);
            GL.Begin(PrimitiveType.Points);
            for (int i = -8; i < 0; i++) GL.Vertex2((i * 32), down);
            for (int i = 1; i < 9; i++) GL.Vertex2((i * 32), down);
            GL.End();

            GL.PointSize(4.0f);

            //GL.Translate(0, 0, 0.01);
            //red left side
            GL.Color3(0.9750f, 0.0f, 0.0f);
            GL.Begin(PrimitiveType.Points);
            for (int i = -8; i < 0; i++) GL.Vertex2((i * 32), down);

            //green right side
            GL.Color3(0.0f, 0.9750f, 0.0f);
            for (int i = 1; i < 9; i++) GL.Vertex2((i * 32), down);
            GL.End();

            //Are you on the right side of line? So its green.
            //GL.Translate(0, 0, 0.01);
            if ((offlineDistance) < 0.0)
            {
                int dots = (dotDistance * -1 / lightbarCmPerPixel);

                GL.PointSize(24.0f);
                GL.Color3(0.0f, 0.0f, 0.0f);
                GL.Begin(PrimitiveType.Points);
                for (int i = 1; i < dots + 1; i++) GL.Vertex2((i * 32), down);
                GL.End();

                GL.PointSize(16.0f);
                GL.Color3(0.0, 1.0, 0.0);
                GL.Begin(PrimitiveType.Points);
                for (int i = 0; i < dots; i++) GL.Vertex2((i * 32 + 32), down);
                GL.End();
                //return;
            }
            else
            {
                int dots = (int)(dotDistance / lightbarCmPerPixel);

                GL.PointSize(24.0f);
                GL.Color3(0.0f, 0.0f, 0.0f);
                GL.Begin(PrimitiveType.Points);
                for (int i = 1; i < dots + 1; i++) GL.Vertex2((i * -32), down);
                GL.End();

                GL.PointSize(16.0f);
                GL.Color3(1.0, 0.3, 0.0);
                GL.Begin(PrimitiveType.Points);
                for (int i = 0; i < dots; i++) GL.Vertex2((i * -32 - 32), down);
                GL.End();
                //return;
            }
            
            //yellow center dot
            if (dotDistance >= -lightbarCmPerPixel && dotDistance <= lightbarCmPerPixel)
            {
                GL.PointSize(32.0f);                
                GL.Color3(0.0f, 0.0f, 0.0f);
                GL.Begin(PrimitiveType.Points);
                GL.Vertex2(0, down);
                //GL.Vertex(0, down + 50);
                GL.End();

                GL.PointSize(24.0f);
                GL.Color3(0.980f, 0.98f, 0.0f);
                GL.Begin(PrimitiveType.Points);
                GL.Vertex2(0, down);
                //GL.Vertex(0, down + 50);
                GL.End();
            }
            else
            {
                GL.PointSize(12.0f);
                GL.Color3(0.0f, 0.0f, 0.0f);
                GL.Begin(PrimitiveType.Points);
                GL.Vertex2(0, down);
                //GL.Vertex(0, down + 50);
                GL.End();

                GL.PointSize(8.0f);
                GL.Color3(0.980f, 0.98f, 0.0f);
                GL.Begin(PrimitiveType.Points);
                GL.Vertex2(0, down);
                //GL.Vertex(0, down + 50);
                GL.End();
            }
        }

        private void DrawLightBarText()
        {
            GL.Disable(EnableCap.DepthTest);

            if (Guidance.BtnGuidanceOn)
            {
                double dist = distanceDisplay * 0.1;

                if (!isMetric) dist *= 0.3937;

                DrawLightBar(oglMain.Width, oglMain.Height, dist);

                double size = 1.5;
                string hede;

                if (dist != 3200 && dist != 3202)
                {
                    if (dist > 0.0)
                    {
                        
                        GL.Color3(1.0, 0.3, 0.0);
                        hede = "< " + (Math.Abs(dist)).ToString("N0");
                    }
                    else
                    {
                        GL.Color3(0.2, 0.9, 0.3);
                        hede = (Math.Abs(dist)).ToString("N0") + " >";
                    }
                    int center = -(int)(((double)(hede.Length) * 0.5) * 16 * size);
                    font.DrawText(center, 36, hede, size);
                }
            }
        }

        private void DrawRollBar()
        {
            //double set = guidanceLineSteerAngle * 0.01 * (40 / vehicle.maxSteerAngle);
            //double actual = actualSteerAngleDisp * 0.01 * (40 / vehicle.maxSteerAngle);
            //double hiit = 0;

            GL.PushMatrix();
            GL.Translate(0, 100, 0);

            GL.LineWidth(1);
            GL.Color3(0.24f, 0.64f, 0.74f);
            double wiid = 60;

            //If roll is used rotate graphic based on roll angle
 
            GL.Begin(PrimitiveType.Lines);
            GL.Vertex2(-wiid - 30,0);
            GL.Vertex2(-wiid-2, 0);
            GL.Vertex2(wiid+2, 0);
            GL.Vertex2(wiid + 30, 0);
            GL.End();
            if (ahrs.rollX16 != 9999)
            {
                GL.Rotate(((ahrs.rollX16 - ahrs.rollZeroX16) * 0.0625f), 0.0f, 0.0f, 1.0f);
            }

            GL.Color3(0.9, 0.9, 0.0);
            GL.LineWidth(2);

            GL.Begin(PrimitiveType.LineStrip);
            GL.Vertex2(-wiid + 10, 15);
            GL.Vertex2(-wiid, 0);
            GL.Vertex2(wiid, 0);
            GL.Vertex2(wiid - 10, 15);
            GL.End();

            string head = "0";

            if (ahrs.rollX16 != 9999) head = Math.Round((ahrs.rollX16 - ahrs.rollZeroX16) * 0.0625, 1).ToString();

            int center = -(int)(((head.Length) * 6));
            font.DrawText(center, 0, head, 0.8);
            


            if (ahrs.rollX16 != 9999)
            {
                GL.Rotate(((ahrs.rollX16 - ahrs.rollZeroX16) * 0.0625f), 0.0f, 0.0f, -1.0f);
            }
            GL.Translate(0, -100, 0);

            //return back
            GL.PopMatrix();
            GL.LineWidth(1);
        }

        private void DrawSky()
        {
            //GL.Translate(0, 0, 0.9);
            ////draw the background when in 3D
            if (camera.camPitch < -52)
            {
                //-10 to -32 (top) is camera pitch range. Set skybox to line up with horizon 
                double hite = (camera.camPitch + 66) * -0.025;

                //the background
                double winLeftPos = -(double)oglMain.Width / 2;
                double winRightPos = -winLeftPos;

                if (isDay)
                {
                    GL.Color3(0.75, 0.75, 0.75);
                    GL.BindTexture(TextureTarget.Texture2D, texture[0]);        // Select Our Texture
                }
                else
                {
                    GL.Color3(0.5, 0.5, 0.5);
                    GL.BindTexture(TextureTarget.Texture2D, texture[10]);        // Select Our Texture
                }
                
                GL.Enable(EnableCap.Texture2D);

                double u = (fixHeading)/Glm.twoPI;
                GL.Begin(PrimitiveType.TriangleStrip);              // Build Quad From A Triangle Strip
                GL.TexCoord2(u+0.25,      0); GL.Vertex2(winRightPos, 0.0); // Top Right
                GL.TexCoord2(u, 0); GL.Vertex2(winLeftPos, 0.0); // Top Left
                GL.TexCoord2(u+0.25,      1); GL.Vertex2(winRightPos, hite * oglMain.Height); // Bottom Right
                GL.TexCoord2(u, 1); GL.Vertex2(winLeftPos, hite * oglMain.Height); // Bottom Left
                GL.End();                       // Done Building Triangle Strip

                //GL.BindTexture(TextureTarget.Texture2D, texture[3]);		// Select Our Texture
                // GL.Translate(400, 200, 0);
                //GL.Rotate(camHeading, 0, 0, 1);
                //GL.Begin(PrimitiveType.TriangleStrip);				// Build Quad From A Triangle Strip
                //GL.TexCoord2(1, 0); GL.Vertex2(0.1 * winRightPos, -0.1 * Height); // Top Right
                //GL.TexCoord2(0, 0); GL.Vertex2(0.1 * winLeftPos, -0.1 * Height); // Top Left
                //GL.TexCoord2(1, 1); GL.Vertex2(0.1 * winRightPos, 0.1 * Height); // Bottom Right
                //GL.TexCoord2(0, 1); GL.Vertex2(0.1 * winLeftPos,  0.1 * Height); // Bottom Left
                //GL.End();						// Done Building Triangle Strip

                //disable, straight color
                GL.Disable(EnableCap.Texture2D);
            }
        }

        private void DrawCompassText()
        {
            int center = oglMain.Width / -2 ;
            int height = 5;

            GL.Color3(0.9752f, 0.952f, 0.0f);
            font.DrawText(center, height, "Beta v4.4.01", 1.0);
            height += 30;

            GL.Color3(0.9752f, 0.952f, 0.93f);
            font.DrawText(center, height, "G: " + (fixHeading * 57.2957795).ToString("N1"), 1.0);
            height += 30;

            if (isCompassOn && (ahrs.isHeadingCorrectionFromBrick || ahrs.isHeadingCorrectionFromAutoSteer))
            {
                if (ahrs.correctionHeadingX16 == 9999)
                {
                    font.DrawText(center, height, "I: ***", 1.0);
                }
                else
                    font.DrawText(center, height, "I:" + Math.Round(ahrs.correctionHeadingX16 * 0.0625, 1).ToString(), 1.0);
                height += 30;
            }
        }

        private void DrawCompass(bool tt)
        {
            //Heading text
            int center = oglMain.Width / 2 - 65;
            font.DrawText(center-8, tt ? 155 : 25, "^", 0.8);

            GL.Enable(EnableCap.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, texture[6]);        // Select Our Texture
            GL.Color4(0.952f, 0.870f, 0.73f, 0.8);

            GL.Translate(center, tt ? 195 : 65, 0);

            GL.Rotate(-camHeading, 0, 0, 1);
            GL.Begin(PrimitiveType.Quads);              // Build Quad From A Triangle Strip
            {
                GL.TexCoord2(0, 0); GL.Vertex2(-60, -60); // 
                GL.TexCoord2(1, 0); GL.Vertex2(60, -60); // 
                GL.TexCoord2(1, 1); GL.Vertex2(60, 60); // 
                GL.TexCoord2(0, 1); GL.Vertex2(-60, 60); //
            }
            GL.End();

            GL.Rotate(-camHeading, 0, 0, -1);
            GL.Translate(-center, tt ? -195 : -65, 0);

            GL.Disable(EnableCap.Texture2D);
        }

        private void DrawLiftIndicator()
        {
            GL.PushMatrix();
            GL.Enable(EnableCap.Texture2D);

            GL.BindTexture(TextureTarget.Texture2D, texture[9]);        // Select Our Texture

            GL.Translate(oglMain.Width / 2 - 35, oglMain.Height/2, 0);

            if (mc.Send_HydraulicLift[5] == 2)
            {
                GL.Color3(0.0f, 0.950f, 0.0f);
            }
            else
            {
                GL.Rotate(180, 0, 0, 1);
                GL.Color3(0.952f, 0.40f, 0.0f);
            }

            GL.Begin(PrimitiveType.Quads);              // Build Quad From A Triangle Strip
            {
                GL.TexCoord2(0, 0); GL.Vertex2(-48, -64); // 
                GL.TexCoord2(1, 0); GL.Vertex2(48, -64.0); // 
                GL.TexCoord2(1, 1); GL.Vertex2(48, 64); // 
                GL.TexCoord2(0, 1); GL.Vertex2(-48, 64); //
            }
            GL.End();

            GL.Disable(EnableCap.Texture2D);
            GL.PopMatrix();
        }

        private void DrawSpeedo()
        {
            GL.Enable(EnableCap.Texture2D);

            GL.BindTexture(TextureTarget.Texture2D, texture[7]);        // Select Our Texture
            GL.Color4(0.952f, 0.980f, 0.98f, 0.99);

            GL.Translate(oglMain.Width / 2 - 65, 65, 0);

            GL.Begin(PrimitiveType.Quads);              // Build Quad From A Triangle Strip
            {
                GL.TexCoord2(0, 0); GL.Vertex2(-58, -58); // 
                GL.TexCoord2(1, 0); GL.Vertex2(58, -58.0); // 
                GL.TexCoord2(1, 1); GL.Vertex2(58, 58); // 
                GL.TexCoord2(0, 1); GL.Vertex2(-58, 58); //
            }
            GL.End();
            GL.BindTexture(TextureTarget.Texture2D, texture[8]);        // Select Our Texture

            double angle = 0;
            if (isMetric)
            {
                double aveSpd = Math.Abs(avgSpeed);
                if (aveSpd > 20) aveSpd = 20;
                angle = (aveSpd - 10) * 15;
            }
            else
            {
                double aveSpd = Math.Abs(avgSpeed*0.62137);
                if (aveSpd > 20) aveSpd = 20;
                angle = (aveSpd - 10) * 15;
            }

            if (pn.speed > -0.1) GL.Color3(0.0f, 0.950f, 0.0f);
            else GL.Color3(0.952f, 0.0f, 0.0f);

            GL.Rotate(angle, 0, 0, 1);
            GL.Begin(PrimitiveType.Quads);              // Build Quad From A Triangle Strip
            {
                GL.TexCoord2(0, 0); GL.Vertex2(-48, -48); // 
                GL.TexCoord2(1, 0); GL.Vertex2(48, -48.0); // 
                GL.TexCoord2(1, 1); GL.Vertex2(48, 48); // 
                GL.TexCoord2(0, 1); GL.Vertex2(-48, 48); //
            }
            GL.End();

            GL.Disable(EnableCap.Texture2D);
            GL.Rotate(angle, 0, 0, -1);
            GL.Translate(-(oglMain.Width / 2 - 65), -65, 0);

        }
        private void DrawLostRTK()
        {
            GL.Color3(0.9752f, 0.52f, 0.0f);
            font.DrawText(-oglMain.Width / 4, 150, "Lost RTK", 2.0);
        }

        public void CalcFrustum()
        {
            float[] proj = new float[16];							// For Grabbing The PROJECTION Matrix
            float[] modl = new float[16];							// For Grabbing The MODELVIEW Matrix
            float[] clip = new float[16];							// Result Of Concatenating PROJECTION and MODELVIEW

            GL.GetFloat(GetPName.ProjectionMatrix, proj);	// Grab The Current PROJECTION Matrix
            GL.GetFloat(GetPName.Modelview0MatrixExt, modl);   // Grab The Current MODELVIEW Matrix  
            
            // Concatenate (Multiply) The Two Matricies
            clip[0] = modl[0] * proj[0] + modl[1] * proj[4] + modl[2] * proj[8] + modl[3] * proj[12];
            clip[1] = modl[0] * proj[1] + modl[1] * proj[5] + modl[2] * proj[9] + modl[3] * proj[13];
            clip[2] = modl[0] * proj[2] + modl[1] * proj[6] + modl[2] * proj[10] + modl[3] * proj[14];
            clip[3] = modl[0] * proj[3] + modl[1] * proj[7] + modl[2] * proj[11] + modl[3] * proj[15];

            clip[4] = modl[4] * proj[0] + modl[5] * proj[4] + modl[6] * proj[8] + modl[7] * proj[12];
            clip[5] = modl[4] * proj[1] + modl[5] * proj[5] + modl[6] * proj[9] + modl[7] * proj[13];
            clip[6] = modl[4] * proj[2] + modl[5] * proj[6] + modl[6] * proj[10] + modl[7] * proj[14];
            clip[7] = modl[4] * proj[3] + modl[5] * proj[7] + modl[6] * proj[11] + modl[7] * proj[15];

            clip[8] = modl[8] * proj[0] + modl[9] * proj[4] + modl[10] * proj[8] + modl[11] * proj[12];
            clip[9] = modl[8] * proj[1] + modl[9] * proj[5] + modl[10] * proj[9] + modl[11] * proj[13];
            clip[10] = modl[8] * proj[2] + modl[9] * proj[6] + modl[10] * proj[10] + modl[11] * proj[14];
            clip[11] = modl[8] * proj[3] + modl[9] * proj[7] + modl[10] * proj[11] + modl[11] * proj[15];

            clip[12] = modl[12] * proj[0] + modl[13] * proj[4] + modl[14] * proj[8] + modl[15] * proj[12];
            clip[13] = modl[12] * proj[1] + modl[13] * proj[5] + modl[14] * proj[9] + modl[15] * proj[13];
            clip[14] = modl[12] * proj[2] + modl[13] * proj[6] + modl[14] * proj[10] + modl[15] * proj[14];
            clip[15] = modl[12] * proj[3] + modl[13] * proj[7] + modl[14] * proj[11] + modl[15] * proj[15];


            // Extract the RIGHT clipping plane
            frustum[0] = clip[3] - clip[0];
            frustum[1] = clip[7] - clip[4];
            frustum[2] = clip[11] - clip[8];
            frustum[3] = clip[15] - clip[12];

            // Extract the LEFT clipping plane
            frustum[4] = clip[3] + clip[0];
            frustum[5] = clip[7] + clip[4];
            frustum[6] = clip[11] + clip[8];
            frustum[7] = clip[15] + clip[12];

            // Extract the FAR clipping plane
            frustum[8] = clip[3] - clip[2];
            frustum[9] = clip[7] - clip[6];
            frustum[10] = clip[11] - clip[10];
            frustum[11] = clip[15] - clip[14];


            // Extract the NEAR clipping plane.  This is last on purpose (see pointinfrustum() for reason)
            frustum[12] = clip[3] + clip[2];
            frustum[13] = clip[7] + clip[6];
            frustum[14] = clip[11] + clip[10];
            frustum[15] = clip[15] + clip[14];

            // Extract the BOTTOM clipping plane
            frustum[16] = clip[3] + clip[1];
            frustum[17] = clip[7] + clip[5];
            frustum[18] = clip[11] + clip[9];
            frustum[19] = clip[15] + clip[13];

            // Extract the TOP clipping plane
            frustum[20] = clip[3] - clip[1];
            frustum[21] = clip[7] - clip[5];
            frustum[22] = clip[11] - clip[9];
            frustum[23] = clip[15] - clip[13];
        }

        public double maxFieldX = 0, maxFieldY = 0, minFieldX = 0, minFieldY = 0, fieldCenterX, fieldCenterY, maxFieldDistance = 1500, maxCrossFieldLength = 1000;

        //determine mins maxs of patches and whole field.
        public void CalculateMinMax()
        {
            minFieldX = 9999999; minFieldY = 9999999;
            maxFieldX = -9999999; maxFieldY = -9999999;

            //min max of the boundary
            if (bnd.Boundaries.Count > 0)
            {
                minFieldY = bnd.Boundaries[0].Northingmin;
                maxFieldY = bnd.Boundaries[0].Northingmax;
                minFieldX = bnd.Boundaries[0].Eastingmin;
                maxFieldX = bnd.Boundaries[0].Eastingmax;
            }
            else
            {
                //for every new chunk of patch
                foreach (var triList in PatchDrawList)
                {
                    int count2 = triList.Count;
                    for (int k = 1; k < count2; k += 3)
                    {
                        double x = triList[k].Easting;
                        double y = triList[k].Northing;

                        //also tally the max/min of field x and z
                        if (minFieldX > x) minFieldX = x;
                        if (maxFieldX < x) maxFieldX = x;
                        if (minFieldY > y) minFieldY = y;
                        if (maxFieldY < y) maxFieldY = y;
                    }
                }
                for (int i = 0; i < Tools.Count; i++)
                {
                    // the follow up to sections patches
                    for (int j = 0; j < Tools[i].Sections.Count; j++)
                    {
                        int patchCount = Tools[i].Sections[j].triangleList.Count;
                        for (int k = 1; k < patchCount; k++)
                        {
                            double x = Tools[i].Sections[j].triangleList[k].Easting;
                            double y = Tools[i].Sections[j].triangleList[k].Northing;

                            //also tally the max/min of field x and z
                            if (minFieldX > x) minFieldX = x;
                            if (maxFieldX < x) maxFieldX = x;
                            if (minFieldY > y) minFieldY = y;
                            if (maxFieldY < y) maxFieldY = y;
                        }
                    }
                }
            }

            if (maxFieldX == -9999999 | minFieldX == 9999999 | maxFieldY == -9999999 | minFieldY == 9999999)
            {
                maxFieldX = 0; minFieldX = 0; maxFieldY = 0; minFieldY = 0; maxFieldDistance = 1500; maxCrossFieldLength = 1000;
            }
            else
            {
                //the largest distancew across field
                double dist = Math.Abs(minFieldX - maxFieldX);
                double dist2 = Math.Abs(minFieldY - maxFieldY);

                maxCrossFieldLength = Math.Sqrt(dist * dist + dist2 * dist2) * 1.05;

                maxFieldDistance = (dist > dist2) ? (dist) : (dist2);

                if (maxFieldDistance < 100) maxFieldDistance = 100;

                fieldCenterX = (maxFieldX + minFieldX) / 2.0;
                fieldCenterY = (maxFieldY + minFieldY) / 2.0;
            }
        }

        private void DrawFieldText()
        {
            if (isMetric)
            {
                if (bnd.Boundaries.Count > 0)
                {
                    sb.Clear();
                    sb.Append(((fd.workedAreaTotal - fd.actualAreaCovered) * Glm.m2ha).ToString("N3"));
                    sb.Append("Ha ");
                    sb.Append(fd.overlapPercent.ToString("N2"));
                    sb.Append("%  ");
                    sb.Append((fd.areaBoundaryOuterLessInner * Glm.m2ha).ToString("N2"));
                    sb.Append("-");
                    sb.Append((fd.actualAreaCovered * Glm.m2ha).ToString("N2"));
                    sb.Append(" = ");
                    sb.Append(((fd.areaBoundaryOuterLessInner - fd.actualAreaCovered) * Glm.m2ha).ToString("N2"));
                    sb.Append("Ha  ");
                    sb.Append(fd.TimeTillFinished);
                    GL.Color3(0.95, 0.95, 0.95);
                    font.DrawText(-sb.Length * 7, oglMain.Height - 32, sb.ToString());
                }
                else
                {
                    sb.Clear();
                    //sb.Append("Overlap ");
                    sb.Append(fd.overlapPercent.ToString("N3"));
                    sb.Append("%   ");
                    sb.Append((fd.actualAreaCovered * Glm.m2ha).ToString("N3"));
                    sb.Append("Ha");
                    GL.Color3(0.95, 0.95, 0.95);
                    font.DrawText(0, oglMain.Height - 32, sb.ToString());
                }
            }
            else
            {
                if (bnd.Boundaries.Count > 0)
                {
                    sb.Clear();
                    sb.Append(((fd.workedAreaTotal - fd.actualAreaCovered) * Glm.m2ac).ToString("N3"));
                    sb.Append("Ac ");
                    sb.Append(fd.overlapPercent.ToString("N2"));
                    sb.Append("%  ");
                    sb.Append((fd.areaBoundaryOuterLessInner * Glm.m2ac).ToString("N2"));
                    sb.Append("-");
                    sb.Append((fd.actualAreaCovered * Glm.m2ac).ToString("N2"));
                    sb.Append(" = ");
                    sb.Append(((fd.areaBoundaryOuterLessInner - fd.actualAreaCovered) * Glm.m2ac).ToString("N2"));
                    sb.Append("Ac  ");
                    sb.Append(fd.TimeTillFinished);
                    GL.Color3(0.95, 0.95, 0.95);
                    font.DrawText(-sb.Length * 7, oglMain.Height - 32, sb.ToString());
                }
                else
                {
                    sb.Clear();
                    //sb.Append("Overlap ");
                    sb.Append(fd.overlapPercent.ToString("N3"));
                    sb.Append("%   ");
                    sb.Append((fd.actualAreaCovered * Glm.m2ac).ToString("N3"));
                    sb.Append("Ac");
                    GL.Color3(0.95, 0.95, 0.95);
                    font.DrawText(0, oglMain.Height - 32, sb.ToString());
                }
            }
        }
    }
}
