import CounterView from './CounterView';

const { React } = window.__dhSdk;

export default function HelloWidget() {
  return React.createElement(CounterView, { compact: true });
}
