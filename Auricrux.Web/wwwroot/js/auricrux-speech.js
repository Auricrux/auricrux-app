window.auricruxSpeech = {
  recognition: null,
  start: function (dotNetRef) {
    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognition) {
      dotNetRef.invokeMethodAsync('OnSpeechError', 'Speech recognition not supported in this browser.');
      return false;
    }
    if (window.auricruxSpeech.recognition) {
      try { window.auricruxSpeech.recognition.stop(); } catch (_) { }
    }
    const rec = new SpeechRecognition();
    window.auricruxSpeech.recognition = rec;
    rec.continuous = false;
    rec.interimResults = false;
    rec.lang = 'en-US';
    rec.onresult = (event) => {
      const text = event.results[0][0].transcript;
      dotNetRef.invokeMethodAsync('OnSpeechResult', text);
    };
    rec.onerror = (event) => {
      dotNetRef.invokeMethodAsync('OnSpeechError', event.error || 'speech-error');
    };
    rec.start();
    return true;
  },
  stop: function () {
    if (window.auricruxSpeech.recognition) {
      try { window.auricruxSpeech.recognition.stop(); } catch (_) { }
    }
  },
  speak: function (text) {
    if (!window.speechSynthesis) return false;
    window.speechSynthesis.cancel();
    const utter = new SpeechSynthesisUtterance(text);
    utter.rate = 1.0;
    window.speechSynthesis.speak(utter);
    return true;
  }
};
