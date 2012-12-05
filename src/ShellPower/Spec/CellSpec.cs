﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace SSCP.ShellPower {
    public class CellSpec {
        public const double STC_TEMP = 25.0;
        public const double STC_INSOLATION = 1000.0;

        /// <summary>
        /// Open-circuit voltage at Standard Test Conditions.
        /// </summary>
        public double VocStc { get; set; }
        /// <summary>
        /// Short-circuit current at Standard Test Conditions.
        /// </summary>
        public double IscStc { get; set; }
        /// <summary>
        /// Change in Voc per degree increase in temperature.
        /// </summary>
        public double DVocDT { get; set; }
        /// <summary>
        /// Change in Isc per degree increase in temperature.
        /// </summary>
        public double DIscDT { get; set; }
        /// <summary>
        /// Cell temperature in Celsius.
        /// </summary>
        public double Temperature { get; set; }
        /// <summary>
        /// Cell area in square meters.
        /// </summary>
        public double Area { get; set; }
        /// <summary>
        /// Diode ideality constant. 1.0 = ideal diode, larger = worse.
        /// </summary>
        public double NIdeal { get; set; }
        /// <summary>
        /// Cell series resistance in Ohms. Usually ~10 milliohms for a silicon pv cell.
        /// </summary>
        public double SeriesR { get; set; }

        public double CalcTempK() {
            return Temperature + Constants.C_IN_KELVIN;
        }
        public double CalcVoc(double insolation) {
            // TODO: adjust for insolation
            return VocStc + (Temperature - STC_TEMP) * DVocDT;
        }
        public double CalcIsc(double insolation) {
            return insolation / STC_INSOLATION * (IscStc + (Temperature - STC_TEMP) * DIscDT);
        }

        /// <summary>
        /// Reverse saturation current for a cell.
        /// </summary>
        public double CalcI0(double insolation){
            double voc = CalcVoc(insolation);
            double isc = CalcIsc(insolation);
            double t = CalcTempK();
            double k = Constants.BOLTZMANN_K;
            double q = Constants.ELECTRON_CHARGE_Q;
            double i0 = isc / (Math.Exp((q*voc) / (NIdeal*k*t)) - 1.0);
            return i0;
        }
        /// <summary>
        /// Calculates current flow for a given voltage.
        /// </summary>
        public double CalcI(double insolation, double v) {
            double[] veci = CalcIV(insolation, new double[] { v });
            return veci[0];
        }
        /// <summary>
        /// Computes an IV curve.
        /// </summary>
        public double[] CalcIV(double insolation, double[] vecv) {
            double[] veci = new double[vecv.Length];
            double i0 = CalcI0(insolation);
            double isc = CalcIsc(insolation);
            double t = CalcTempK();
            double k = Constants.BOLTZMANN_K;
            double q = Constants.ELECTRON_CHARGE_Q;
            double ni = NIdeal;
            double rs = SeriesR;
            for (int i = 0; i < vecv.Length; i++) {
                double v = vecv[i];
                // iterate to convergence
                double iprev = 0.0, icurr = 0.0;
                for(int j = 0; j < 2000; j++){
                    double vdrop = iprev * rs;
                    double idark = i0 * (Math.Exp((q * (v + vdrop)) / (ni * k * t)) - 1.0);
                    icurr = isc - idark;
                    if(Math.Abs(icurr-iprev) < 1e-6){
                        Debug.WriteLine("converged in "+j);
                        break;
                    }
                    iprev = 0.95*iprev + 0.05*icurr;
                }
                veci[i] = icurr;
            }
            return veci;
        }
        public void CalcSweep(double insolation, out double ff, out double vmp, out double imp, out double[] veci, out double[] vecv) {
            double voc = CalcVoc(insolation);
            double isc = CalcIsc(insolation);
            int n = 100;
            vecv = new double[n + 1];
            for (int i = 0; i <= n; i++) {
                vecv[i] = voc*(double)i/n;
            }
            veci = CalcIV(insolation, vecv);
            //Debug.Assert(Math.Abs(veci[0] - isc) < 0.001);
            //Debug.Assert(Math.Abs(veci[n]) < 0.001);
            double pmp = 0.0;
            imp = vmp = 0.0;
            for (int i = 0; i < n; i++) {
                double p = veci[i] * vecv[i];
                if (p > pmp) {
                    pmp = p;
                    vmp = vecv[i];
                    imp = veci[i];
                }
            }
            ff = pmp / (isc * voc);
        }
    }
}
