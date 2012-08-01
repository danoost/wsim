﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using RexSimulator.Hardware;
using System.IO;
using RexSimulatorGui.Properties;
using System.Threading;

namespace RexSimulatorGui.Forms
{
    /// <summary>
    /// The main form for the simulator GUI.
    /// </summary>
    public partial class RexBoardForm : Form
    {
        #region Defines
        /// <summary>
        /// The clock rate that the simulator should (try to) run at, if throttling is enabled.
        /// </summary>
        private const long TARGET_CLOCK_RATE = 4000000;
        /// <summary>
        /// Set true if the simulated CPU should not exceed TARGET_CLOCK_RATE.
        /// </summary>
        private const bool THROTTLE_CLOCK_RATE = true;
        #endregion

        #region Member Variables
        private RexBoard mRexBoard;
        private Thread mWorker;

        private BasicSerialPortForm mSerialForm1;
        private BasicSerialPortForm mSerialForm2;
        private RegisterForm mGpRegisterForm;
        private RegisterForm mSpRegisterForm;
        private MemoryForm mRamForm;
        private PeripheralMemoryForm mInterruptButtonForm;
        private PeripheralMemoryForm mSerialConfigForm1;
        private PeripheralMemoryForm mSerialConfigForm2;
        private PeripheralMemoryForm mParallelConfigForm;
        private PeripheralMemoryForm mTimerConfigForm;

        private List<Form> mSubforms;

        private long mLastTickCount = 0;
        private DateTime mLastTickCountUpdate = DateTime.Now;
        public double mLastClockRate = TARGET_CLOCK_RATE;
        private double mLastClockRateSmoothed = TARGET_CLOCK_RATE;
        private long mSlowdownCount = 1;

        private bool mRunning = true;
        private bool mStepping = false;
        #endregion

        #region Constructor
        public RexBoardForm()
        {
            InitializeComponent();

            //Set up all REX and WRAMP hardware
            mRexBoard = rexWidget1.mBoard;

            //Load WRAMPmon into ROM
            Stream wmon = new MemoryStream(Resources.monitor);
            rexWidget1.LoadSrec(wmon);
            wmon.Close();

            //Set up the worker thread
            mWorker = new Thread(new ThreadStart(Worker));

            //Set up all forms
            mSubforms = new List<Form>();
            mSerialForm1 = new BasicSerialPortForm(mRexBoard.Serial1);
            mSerialForm2 = new BasicSerialPortForm(mRexBoard.Serial2);
            mGpRegisterForm = new RegisterForm(mRexBoard.CPU.mGpRegisters, false);
            mSpRegisterForm = new RegisterForm(mRexBoard.CPU.mSpRegisters, true);
            mRamForm = new MemoryForm(mRexBoard.RAM);
            mRamForm.SetCpu(mRexBoard.CPU);
            mInterruptButtonForm = new PeripheralMemoryForm(mRexBoard.InterruptButton);
            mSerialConfigForm1 = new PeripheralMemoryForm(mRexBoard.Serial1);
            mSerialConfigForm2 = new PeripheralMemoryForm(mRexBoard.Serial2);
            mParallelConfigForm = new PeripheralMemoryForm(mRexBoard.Parallel);
            mTimerConfigForm = new PeripheralMemoryForm(mRexBoard.Timer);
            

            //Add all forms to the list of subforms
            mSubforms.Add(mSerialForm1);
            mSubforms.Add(mSerialForm2);
            mSubforms.Add(mGpRegisterForm);
            mSubforms.Add(mSpRegisterForm);
            mSubforms.Add(mRamForm);
            mSubforms.Add(mInterruptButtonForm);
            mSubforms.Add(mSerialConfigForm1);
            mSubforms.Add(mSerialConfigForm2);
            mSubforms.Add(mParallelConfigForm);
            mSubforms.Add(mTimerConfigForm);
            
            //Wire up event handlers
            foreach (Form f in mSubforms)
            {
                f.VisibleChanged += new EventHandler(SubForm_VisibleChanged);
            }

            //Set the GUI update timer going!
            updateTimer.Start();
        }
        #endregion

        #region Thread Workers
        /// <summary>
        /// Functions as the board's clock source.
        /// </summary>
        private void Worker()
        {
            int stepCount = 0;
            int stepsPerSleep = 0;

            while (true)
            {
                if (mRunning)
                {
                    rexWidget1.Step();
                    mRunning ^= mStepping; //stop the CPU running if this is only supposed to do a single step.

                    //Slow the processor down if need be
                    if (THROTTLE_CLOCK_RATE)
                    {
                        if (stepCount++ >= stepsPerSleep)
                        {
                            stepCount -= stepsPerSleep;
                            Thread.Sleep(5);
                            int diff = (int)mLastClockRate - (int)TARGET_CLOCK_RATE;
                            stepsPerSleep -= diff / 10000;
                            stepsPerSleep = Math.Min(Math.Max(0, stepsPerSleep), 1000000);
                        }

                        /*for (long i = 0; i < mSlowdownCount / 1000000000; i++) ; //dirty cpu cycle-wasting loop                        

                        long diff = (long)mLastClockRate - TARGET_CLOCK_RATE;

                        mSlowdownCount += diff;

                        Thread.Sleep(0); //swap thread out to allow other processes to run.*/
                    }
                }
            }
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Show/hide forms if the checkboxes are clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Checkbox_CheckedChanged(object sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                EventHandler d = new EventHandler(Checkbox_CheckedChanged);
                this.Invoke(d, sender, e);
            }
            else
            {
                mSerialForm1.Visible = serialForm1Checkbox.Checked;
                mSerialForm2.Visible = serialForm2Checkbox.Checked;
                mGpRegisterForm.Visible = gprCheckbox.Checked;
                mSpRegisterForm.Visible = sprCheckbox.Checked;
                mRamForm.Visible = memoryCheckbox.Checked;
                mInterruptButtonForm.Visible = interruptButtonCheckbox.Checked;
                mSerialConfigForm1.Visible = serialConfig1Checkbox.Checked;
                mSerialConfigForm2.Visible = serialConfig2Checkbox.Checked;
                mParallelConfigForm.Visible = parallelConfigCheckbox.Checked;
                mTimerConfigForm.Visible = timerConfigCheckbox.Checked;
            }
        }

        /// <summary>
        /// Update checkboxes to reflect the state of all subforms.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SubForm_VisibleChanged(object sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                EventHandler d = new EventHandler(SubForm_VisibleChanged);
                this.Invoke(d, sender, e);
            }
            else
            {
                serialForm1Checkbox.Checked = mSerialForm1.Visible;
                serialForm2Checkbox.Checked = mSerialForm2.Visible;
                gprCheckbox.Checked = mGpRegisterForm.Visible;
                sprCheckbox.Checked = mSpRegisterForm.Visible;
                memoryCheckbox.Checked = mRamForm.Visible;
                interruptButtonCheckbox.Checked = mInterruptButtonForm.Visible;
                serialConfig1Checkbox.Checked = mSerialConfigForm1.Visible;
                serialConfig2Checkbox.Checked = mSerialConfigForm2.Visible;
                parallelConfigCheckbox.Checked = mParallelConfigForm.Visible;
                timerConfigCheckbox.Checked = mTimerConfigForm.Visible;
            }
        }

        private void RexBoardForm_Load(object sender, EventArgs e)
        {
            //Open default forms
            Checkbox_CheckedChanged(this, null);

            //Start the CPU running.
            mWorker.Start();
        }

        /// <summary>
        /// Clean up all threads before closing the program.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RexBoardForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            mWorker.Abort();
            mSerialForm1.KillWorkers();
            mSerialForm2.KillWorkers();
            //Application.Exit();
        }

        /// <summary>
        /// Redraw the REX widget.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RexBoardForm_Paint(object sender, PaintEventArgs e)
        {

            rexWidget1.Invalidate();
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Recalculate the simulated CPU clock rate.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void updateTimer_Tick(object sender, EventArgs e)
        {
            long ticksSinceLastUpdate = mRexBoard.TickCounter - mLastTickCount;
            TimeSpan timeSinceLastUpdate = DateTime.Now.Subtract(mLastTickCountUpdate);
            mLastTickCount = mRexBoard.TickCounter;
            mLastTickCountUpdate = DateTime.Now;

            double rate = 0.5;
            mLastClockRate = ticksSinceLastUpdate / timeSinceLastUpdate.TotalSeconds;
            mLastClockRateSmoothed = mLastClockRateSmoothed * (1.0 - rate) + mLastClockRate * rate;

            this.Text = string.Format("REX Board Simulator: Clock Rate: {0:0.000} MHz ({1:000}%)", mLastClockRateSmoothed / 1e6, mLastClockRateSmoothed * 100 / TARGET_CLOCK_RATE);
            rexWidget1.Invalidate();
        }

        /// <summary>
        /// Set the CPU running, or halted.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void runButton_Click(object sender, EventArgs e)
        {
            mStepping = false;
            mRunning ^= true;
            ((Button)sender).Text = mRunning ? "Stop" : "Run";
            ((Button)sender).BackColor = mRunning ? Color.Green : Color.Red;
        }

        /// <summary>
        /// Single-step the WRAMP program.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void stepButton_Click(object sender, EventArgs e)
        {
            if (mRunning)
                runButton.PerformClick();
            mStepping = true;
            mRunning = true;
        }
        #endregion
    }
}
