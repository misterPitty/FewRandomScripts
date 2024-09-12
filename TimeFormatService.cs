using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AlexeyH.Common.Universal
{
    public enum TimeLocalizationFormat
    {
        DoubleDot,
        HintExpire2,
        Common
    }

    public class TimeFormatService: ITimeFormatter
    {
        private Dictionary<TimeLocalizationFormat, string[]> _localizedSuffixes;

        private TimeSpan _timeSpan;
        private StringBuilder _stringBuilder = new();
        private int _segmentsCount;

        private const string _dTemplate = "{0:dd}{1}";
        private const string _hTemplate = "{0:hh}{1}";
        private const string _mTemplate = "{0:mm}{1}";
        private const string _sTemplate = "{0:ss}{1}";

        [Inject]
        private void Construct(IRawLocalizationDataProvider localization)
        {
            SetSuffixes(localization);
        }

        private void SetSuffixes(IRawLocalizationDataProvider localization)
        {
            _localizedSuffixes = new();

            _localizedSuffixes[TimeLocalizationFormat.DoubleDot] = new string[4] { ":", ":", ":", ":" };

            _localizedSuffixes[TimeLocalizationFormat.HintExpire2] = new string[4]
            {
                localization.GetLocalizedString("hint.expire_days2") + " ",
                localization.GetLocalizedString("hint.expire_hours2") + " ",
                localization.GetLocalizedString("hint.expire_minutes2") + " ",
                localization.GetLocalizedString("hint.expire_seconds2")
            };

            _localizedSuffixes[TimeLocalizationFormat.Common] = new string[4]
            {
                localization.GetLocalizedString("common.days") + " ",
                localization.GetLocalizedString("common.hours") + " ",
                localization.GetLocalizedString("common.minutes") + " ",
                localization.GetLocalizedString("common.seconds")
            };
        }

        public string Format(in long seconds, in TimeLocalizationFormat format, in int maxSegments = 2)
        {
            if (_localizedSuffixes == null)
            {
                throw new Exception("[TimeUtils] LocalizedSuffixes isn't setted");
            }

            _timeSpan = TimeSpan.FromSeconds(seconds);
            _stringBuilder.Clear();
            _segmentsCount = 0;

            if (_timeSpan.TotalDays >= 1 && _segmentsCount < maxSegments)
            {
                _stringBuilder.AppendFormat(_dTemplate, _timeSpan, _localizedSuffixes[format][0]);
                _segmentsCount++;
            }

            if (_timeSpan.TotalHours >= 1 && _segmentsCount < maxSegments)
            {
                _stringBuilder.AppendFormat(_hTemplate, _timeSpan, _localizedSuffixes[format][1]);
                _segmentsCount++;
            }

            if (_timeSpan.TotalMinutes >= 1 && _segmentsCount < maxSegments)
            {
                _stringBuilder.AppendFormat(_mTemplate, _timeSpan, _localizedSuffixes[format][2]);
                _segmentsCount++;
            }

            if (_timeSpan.TotalSeconds >= 1 && _segmentsCount < maxSegments)
            {
                _stringBuilder.AppendFormat(_sTemplate, _timeSpan, _localizedSuffixes[format][3]);
                _segmentsCount++;
            }

            if (format == TimeLocalizationFormat.DoubleDot)
            {
                _stringBuilder.Remove(_stringBuilder.Length - 1, 1);
            }

            return _stringBuilder.ToString();
        }
    }
}
