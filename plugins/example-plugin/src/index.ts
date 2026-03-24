import HelloWidget from './HelloWidget';
import HelloPage from './HelloPage';

const { plugin } = window.__dhSdk;

plugin.registerWidget('com.example.hello-plugin.hello-widget', HelloWidget);
plugin.registerRoute('/plugins/hello', HelloPage);
