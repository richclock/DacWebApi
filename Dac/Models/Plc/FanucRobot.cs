using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Dac.Interfaces;
using Dac.Models.Config;

using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using FRRJIf;

namespace Dac.Models.Plc
{
    public class FanucRobot : IPlc
    {
        private IConfig _config = null;
        private bool _isConnected = false;
        private bool disposedValue;
        private System.Text.Encoding encode = System.Text.Encoding.Default;

        public string HostName;

        private const string cnstApp = "frrjiftest";
        private const string cnstSection = "setting";

        private Random rnd = new Random();

        private FRRJIf.Core mobjCore;
        private FRRJIf.DataTable mobjDataTable;
        private FRRJIf.DataCurPos mobjCurPos;
        private FRRJIf.DataCurPos mobjCurPosUF;
        private FRRJIf.DataCurPos mobjCurPos2;
        private FRRJIf.DataCurPos mobjCurPos3;
        private FRRJIf.DataCurPos mobjCurPos4;
        private FRRJIf.DataCurPos mobjCurPos5;
        private FRRJIf.DataTask mobjTask;
        private FRRJIf.DataTask mobjTaskIgnoreMacro;
        private FRRJIf.DataTask mobjTaskIgnoreKarel;
        private FRRJIf.DataTask mobjTaskIgnoreMacroKarel;
        private FRRJIf.DataPosReg mobjPosReg;
        private FRRJIf.DataPosReg mobjPosReg2;
        private FRRJIf.DataPosReg mobjPosReg3;
        private FRRJIf.DataPosReg mobjPosReg4;
        private FRRJIf.DataPosReg mobjPosReg5;
        private FRRJIf.DataPosRegXyzwpr mobjPosRegXyzwpr;
        private FRRJIf.DataPosRegMG mobjPosRegMG;
        private FRRJIf.DataSysVar mobjSysVarInt;
        private FRRJIf.DataSysVar mobjSysVarInt2;
        private FRRJIf.DataSysVar mobjSysVarReal;
        private FRRJIf.DataSysVar mobjSysVarReal2;
        private FRRJIf.DataSysVar mobjSysVarString;
        private FRRJIf.DataSysVar mobjSysVarString2;
        private FRRJIf.DataSysVarPos mobjSysVarPos;
        private FRRJIf.DataSysVar[] mobjSysVarIntArray;
        private FRRJIf.DataNumReg mobjNumReg;
        private FRRJIf.DataNumReg mobjNumReg2;
        private FRRJIf.DataAlarm mobjAlarm;
        private FRRJIf.DataAlarm mobjAlarmCurrent;
        private FRRJIf.DataAlarm mobjAlarmPasswd;
        private FRRJIf.DataSysVar mobjVarString;
        private FRRJIf.DataString mobjStrReg;
        private FRRJIf.DataString mobjStrRegComment;
        private FRRJIf.DataString mobjDIComment;
        private FRRJIf.DataString mobjDOComment;
        private FRRJIf.DataString mobjRIComment;
        private FRRJIf.DataString mobjROComment;
        private FRRJIf.DataString mobjUIComment;
        private FRRJIf.DataString mobjUOComment;
        private FRRJIf.DataString mobjSIComment;
        private FRRJIf.DataString mobjSOComment;
        private FRRJIf.DataString mobjWIComment;
        private FRRJIf.DataString mobjWOComment;
        private FRRJIf.DataString mobjWSIComment;
        private FRRJIf.DataString mobjAIComment;
        private FRRJIf.DataString mobjAOComment;
        private FRRJIf.DataString mobjGIComment;
        private FRRJIf.DataString mobjGOComment;

        private FRRJIf.DataTable mobjDataTable2;
        private FRRJIf.DataNumReg mobjNumReg3;
        public bool IsConnected => _isConnected;

        public FanucRobot(IOptions<PlcConfig> config)
        {
            _config = config.Value;
        }
        #region 需實作區域
        public bool Connect()
        {
            string ip = _config.IP;
            int port = _config.Port;
            if (mobjCore == null)
            {
                //connect
                HostName = ip;
                msubInit();
            }
            else
            {
                //disconnect
                mobjCore.Disconnect();
                msubDisconnected2();
            }

            return true;
        }

        public bool Disconnect()
        {
            return true;
        }
        public bool ReadCoil(int address)
        {
            return true;
        }

        public int ReadHoldingReg(int address)
        {
            return 1;
        }

        public int ReadHoldingReg32bit(int address)
        {
            return 1;
        }

        public bool WriteCoil(int address, bool value)
        {
            return true;
        }

        public bool WriteHoldingReg32bit(int address, int value)
        {
            return true;
        }

        public bool WriteHoldingReg16bit(int address, int value)
        {
            return true;
        }
        #endregion
        #region Async Function
        public async Task<bool> ReadCoilAsync(int address)
        {
            return await Task.Run(() =>
            {
                return ReadCoil(address);
            });
        }

        public async Task<int> ReadHoldingRegAsync(int address)
        {
            return await Task.Run(() =>
            {
                return ReadHoldingReg(address);
            });
        }

        public async Task<int> ReadHoldingReg32bitAsync(int address)
        {
            return await Task.Run(() =>
            {
                return ReadHoldingReg32bit(address);
            });
        }

        public async Task<bool> WriteCoilAsync(int address, bool value)
        {
            return await Task.Run(() =>
            {
                return WriteCoil(address, value);
            });
        }

        public async Task<bool> WriteHoldingReg32bitAsync(int address, int value)
        {
            return await Task.Run(() =>
            {
                return WriteHoldingReg32bit(address, value);
            });
        }

        public async Task<bool> WriteHoldingReg16bitAsync(int address, int value)
        {
            return await Task.Run(() =>
            {
                return WriteHoldingReg16bit(address, value);
            });
        }

        public async Task<bool> ConnectAsync()
        {
            return await Task.Run(() =>
            {
                return Connect();
            });
        }

        public async Task<bool> DisconnectAsync()
        {
            return await Task.Run(() =>
            {
                return Connect();
            });
        }
        #endregion

        private void msubInit()
        {
            bool blnRes = false;
            string strHost = null;
            int lngTmp = 0;

            try
            {


                mobjCore = new FRRJIf.Core(encode);

                // You need to set data table before connecting.
                mobjDataTable = mobjCore.get_DataTable();

                {
                    mobjAlarm = mobjDataTable.AddAlarm(FRRJIf.FRIF_DATA_TYPE.ALARM_LIST, 5, 0);
                    mobjAlarmCurrent = mobjDataTable.AddAlarm(FRRJIf.FRIF_DATA_TYPE.ALARM_CURRENT, 1, 0);
                    mobjAlarmPasswd = mobjDataTable.AddAlarm(FRRJIf.FRIF_DATA_TYPE.ALARM_PASSWORD, 1);
                    //mobjCurPos = mobjDataTable.AddCurPos(FRRJIf.FRIF_DATA_TYPE.CURPOS, 1);
                    mobjCurPos = mobjDataTable.AddCurPosUF(FRRJIf.FRIF_DATA_TYPE.CURPOS, 1, 15);
                    mobjCurPosUF = mobjDataTable.AddCurPosUF(FRRJIf.FRIF_DATA_TYPE.CURPOS, 1, 15);
                    //mobjCurPos2 = mobjDataTable.AddCurPos(FRRJIf.FRIF_DATA_TYPE.CURPOS, 2);
                    mobjCurPos2 = mobjDataTable.AddCurPosUF(FRRJIf.FRIF_DATA_TYPE.CURPOS, 2, 15);
                    mobjCurPos3 = mobjDataTable.AddCurPosUF(FRRJIf.FRIF_DATA_TYPE.CURPOS, 3, 0);
                    mobjCurPos4 = mobjDataTable.AddCurPosUF(FRRJIf.FRIF_DATA_TYPE.CURPOS, 4, 0);
                    mobjCurPos5 = mobjDataTable.AddCurPosUF(FRRJIf.FRIF_DATA_TYPE.CURPOS, 5, 0);
                    mobjTask = mobjDataTable.AddTask(FRRJIf.FRIF_DATA_TYPE.TASK, 1);

                    mobjTaskIgnoreMacro = mobjDataTable.AddTask(FRRJIf.FRIF_DATA_TYPE.TASK_IGNORE_MACRO, 1);
                    mobjTaskIgnoreKarel = mobjDataTable.AddTask(FRRJIf.FRIF_DATA_TYPE.TASK_IGNORE_KAREL, 1);
                    mobjTaskIgnoreMacroKarel = mobjDataTable.AddTask(FRRJIf.FRIF_DATA_TYPE.TASK_IGNORE_MACRO_KAREL, 1);

                    mobjPosReg = mobjDataTable.AddPosReg(FRRJIf.FRIF_DATA_TYPE.POSREG, 1, 1, 10);
                    mobjPosReg2 = mobjDataTable.AddPosReg(FRRJIf.FRIF_DATA_TYPE.POSREG, 2, 1, 4);
                    mobjPosReg3 = mobjDataTable.AddPosReg(FRRJIf.FRIF_DATA_TYPE.POSREG, 3, 1, 10);
                    mobjPosReg4 = mobjDataTable.AddPosReg(FRRJIf.FRIF_DATA_TYPE.POSREG, 4, 1, 10);
                    mobjPosReg5 = mobjDataTable.AddPosReg(FRRJIf.FRIF_DATA_TYPE.POSREG, 5, 1, 10);

                    mobjSysVarInt = mobjDataTable.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_INT, "$FAST_CLOCK");
                    mobjSysVarInt2 = mobjDataTable.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_INT, "$TIMER[10].$TIMER_VAL");
                    mobjSysVarReal = mobjDataTable.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_REAL, "$MOR_GRP[1].$CURRENT_ANG[1]");
                    mobjSysVarReal2 = mobjDataTable.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_REAL, "$DUTY_TEMP");
                    mobjSysVarString = mobjDataTable.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_STRING, "$TIMER[10].$COMMENT");
                    mobjSysVarString2 = mobjDataTable.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_STRING, "$TIMER[2].$COMMENT");
                    mobjSysVarPos = mobjDataTable.AddSysVarPos(FRRJIf.FRIF_DATA_TYPE.SYSVAR_POS, "$MNUTOOL[1,1]");

                    mobjVarString = mobjDataTable.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_STRING, "$[HTTPKCL]CMDS[1]");

                    mobjNumReg = mobjDataTable.AddNumReg(FRRJIf.FRIF_DATA_TYPE.NUMREG_INT, 1, 5);
                    mobjNumReg2 = mobjDataTable.AddNumReg(FRRJIf.FRIF_DATA_TYPE.NUMREG_REAL, 6, 10);
                    mobjPosRegXyzwpr = mobjDataTable.AddPosRegXyzwpr(FRRJIf.FRIF_DATA_TYPE.POSREG_XYZWPR, 1, 1, 10);
                    mobjPosRegMG = mobjDataTable.AddPosRegMG(FRRJIf.FRIF_DATA_TYPE.POSREGMG, "C,J6", 1, 10);

                    mobjDIComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.SDI_COMMENT, 1, 3);
                    mobjDOComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.SDO_COMMENT, 1, 3);
                    mobjRIComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.RDI_COMMENT, 1, 3);
                    mobjROComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.RDO_COMMENT, 1, 3);
                    mobjUIComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.UI_COMMENT, 1, 3);
                    mobjUOComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.UO_COMMENT, 1, 3);
                    mobjSIComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.SI_COMMENT, 1, 3);
                    mobjSOComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.SO_COMMENT, 1, 3);
                    mobjWIComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.WI_COMMENT, 1, 3);
                    mobjWOComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.WO_COMMENT, 1, 3);
                    mobjWSIComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.WSI_COMMENT, 1, 3);
                    mobjAIComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.AI_COMMENT, 1, 3);
                    mobjAOComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.AO_COMMENT, 1, 3);
                    mobjGIComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.GI_COMMENT, 1, 3);
                    mobjGOComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.GO_COMMENT, 1, 3);

                    mobjStrReg = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.STRREG, 1, 3);
                    mobjStrRegComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.STRREG_COMMENT, 1, 3);
                    Debug.Assert(mobjStrRegComment != null);
                }

                // 2nd data table.
                // You must not set the first data table.
                mobjDataTable2 = mobjCore.get_DataTable2();
                mobjNumReg3 = mobjDataTable2.AddNumReg(FRRJIf.FRIF_DATA_TYPE.NUMREG_INT, 1, 5);
                mobjSysVarIntArray = new FRRJIf.DataSysVar[10];
                mobjSysVarIntArray[0] = mobjDataTable2.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_INT, "$TIMER[1].$TIMER_VAL");
                mobjSysVarIntArray[1] = mobjDataTable2.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_INT, "$TIMER[2].$TIMER_VAL");
                mobjSysVarIntArray[2] = mobjDataTable2.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_INT, "$TIMER[3].$TIMER_VAL");
                mobjSysVarIntArray[3] = mobjDataTable2.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_INT, "$TIMER[4].$TIMER_VAL");
                mobjSysVarIntArray[4] = mobjDataTable2.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_INT, "$TIMER[5].$TIMER_VAL");
                mobjSysVarIntArray[5] = mobjDataTable2.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_INT, "$TIMER[6].$TIMER_VAL");
                mobjSysVarIntArray[6] = mobjDataTable2.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_INT, "$TIMER[7].$TIMER_VAL");
                mobjSysVarIntArray[7] = mobjDataTable2.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_INT, "$TIMER[8].$TIMER_VAL");
                mobjSysVarIntArray[8] = mobjDataTable2.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_INT, "$TIMER[9].$TIMER_VAL");
                mobjSysVarIntArray[9] = mobjDataTable2.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_INT, "$TIMER[10].$TIMER_VAL");

                //get host name

                //if (string.IsNullOrEmpty(HostName))
                //{
                //    strHost = Interaction.GetSetting(cnstApp, cnstSection, "HostName", "");
                //    strHost = Interaction.InputBox("Please input robot host name", "frrjiftest", strHost, 0, 0);
                //    if (string.IsNullOrEmpty(strHost))
                //    {
                //        System.Environment.Exit(0);
                //    }
                //    Interaction.SaveSetting(cnstApp, cnstSection, "HostName", strHost);
                //    HostName = strHost;
                //}
                //else
                //{
                //    strHost = HostName;
                //}

                ////get time out value
                //lngTmp = Convert.ToInt32(Interaction.GetSetting(cnstApp, cnstSection, "TimeOut", "-1"));

                //connect
                if (lngTmp > 0)
                    mobjCore.set_TimeOutValue(lngTmp);
                blnRes = mobjCore.Connect(strHost);
                if (blnRes == false)
                {
                    msubDisconnected();
                }
                else
                {
                    msubConnected();
                }

                return;
            }
            catch (Exception ex)
            {
                System.Environment.Exit(0);
            }


        }


        private void msubConnected()
        {


        }

        private void msubDisconnected()
        {


            msubClearVars();


        }

        private void msubDisconnected2()
        {

            //disabled continous


            msubClearVars();



        }


        private void msubClearVars()
        {

            mobjCore.Disconnect();

            mobjCore = null;
            mobjDataTable = null;
            mobjCurPos = null;
            mobjCurPos2 = null;
            mobjCurPos3 = null;
            mobjCurPos4 = null;
            mobjCurPos5 = null;
            mobjTask = null;
            mobjTaskIgnoreMacro = null;
            mobjTaskIgnoreKarel = null;
            mobjTaskIgnoreMacroKarel = null;
            mobjPosReg = null;
            mobjPosReg2 = null;
            mobjPosReg3 = null;
            mobjPosReg4 = null;
            mobjPosReg5 = null;
            mobjPosRegXyzwpr = null;
            mobjPosRegMG = null;
            mobjSysVarInt = null;
            mobjSysVarReal = null;
            mobjSysVarReal2 = null;
            mobjSysVarString = null;
            mobjSysVarString2 = null;
            mobjSysVarPos = null;
            for (int ii = mobjSysVarIntArray.GetLowerBound(0); ii <= mobjSysVarIntArray.GetUpperBound(0); ii++)
            {
                mobjSysVarIntArray[ii] = null;
            }
            mobjNumReg = null;
            mobjNumReg2 = null;
            mobjAlarm = null;
            mobjAlarmCurrent = null;
            mobjAlarmPasswd = null;
            mobjVarString = null;
            mobjStrReg = null;
            mobjStrRegComment = null;
            mobjDIComment = null;
            mobjDOComment = null;
            mobjRIComment = null;
            mobjROComment = null;
            mobjUIComment = null;
            mobjUOComment = null;
            mobjSIComment = null;
            mobjSOComment = null;
            mobjWIComment = null;
            mobjWOComment = null;
            mobjWSIComment = null;
            mobjAIComment = null;
            mobjAOComment = null;
            mobjGIComment = null;
            mobjGOComment = null;
            mobjDataTable2 = null;
            mobjNumReg3 = null;
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 處置受控狀態 (受控物件)
                }

                // TODO: 釋出非受控資源 (非受控物件) 並覆寫完成項
                // TODO: 將大型欄位設為 Null
                Disconnect();
                disposedValue = true;
            }
        }

        // // TODO: 僅有當 'Dispose(bool disposing)' 具有會釋出非受控資源的程式碼時，才覆寫完成項
        // ~TestPlc()
        // {
        //     // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
