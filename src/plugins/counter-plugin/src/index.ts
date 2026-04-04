import CounterWidget from './CounterWidget';
import CounterPage from './CounterPage';

const { plugin } = window.__dhSdk;

plugin.registerWidget('com.example.counter-plugin.counter-widget', CounterWidget);
plugin.registerRoute('/plugins/counter', CounterPage);
