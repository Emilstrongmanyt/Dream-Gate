using System;
using Kindling.Sim.Model;

namespace Kindling.Sim.Match
{
    public static class Glicko2
    {
        public const double DefaultRating = 1500;
        public const double DefaultRd = 350;
        const double Scale = 173.7178;
        const double Tau = 0.5;
        const double Pi2 = Math.PI * Math.PI;

        public static void ApplyPlaces(PlayerState[] seats)
        {
            if (seats == null || seats.Length == 0) return;
            int n = seats.Length;
            var mu = new double[n];
            var phi = new double[n];
            var place = new int[n];
            for (int i = 0; i < n; i++)
            {
                double r = seats[i].Rating > 1 ? seats[i].Rating : DefaultRating;
                double rd = seats[i].Rd > 1 ? seats[i].Rd : DefaultRd;
                mu[i] = (r - DefaultRating) / Scale;
                phi[i] = rd / Scale;
                place[i] = seats[i].Place ?? (n);
            }
            for (int i = 0; i < n; i++)
            {
                double vInv = 0;
                double deltaSum = 0;
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    double g = G(phi[j]);
                    double e = E(mu[i], mu[j], phi[j]);
                    vInv += g * g * e * (1 - e);
                    double s = Score(place[i], place[j]);
                    deltaSum += g * (s - e);
                }
                if (vInv <= 1e-12)
                {
                    seats[i].Rd = Math.Min(DefaultRd, seats[i].Rd + 10);
                    continue;
                }
                double v = 1.0 / vInv;
                double delta = v * deltaSum;
                double phiStar = Math.Sqrt(phi[i] * phi[i] + Tau * Tau);
                double phiP = 1.0 / Math.Sqrt(1.0 / (phiStar * phiStar) + 1.0 / v);
                double muP = mu[i] + phiP * phiP * deltaSum;
                seats[i].Rating = muP * Scale + DefaultRating;
                seats[i].Rd = phiP * Scale;
                if (seats[i].Rd < 30) seats[i].Rd = 30;
                if (seats[i].Rd > DefaultRd) seats[i].Rd = DefaultRd;
            }
        }

        static double Score(int myPlace, int theirPlace)
        {
            if (myPlace < theirPlace) return 1;
            if (myPlace > theirPlace) return 0;
            return 0.5;
        }

        static double G(double phi)
        {
            return 1.0 / Math.Sqrt(1.0 + 3.0 * phi * phi / Pi2);
        }

        static double E(double mu, double muJ, double phiJ)
        {
            return 1.0 / (1.0 + Math.Exp(-G(phiJ) * (mu - muJ)));
        }
    }
}
