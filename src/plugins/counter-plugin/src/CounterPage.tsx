import CounterView from './CounterView';

const { React, ui } = window.__dhSdk;
const { PageRoot, Card } = ui;

export default function HelloPage() {
  return React.createElement(
    PageRoot,
    null,
    React.createElement(
      Card,
      { style: { maxWidth: 320, margin: '2rem auto', padding: '2rem', textAlign: 'center' as const } },
      React.createElement('h2', { style: { margin: '0 0 1.5rem' } }, '🔢 Counter'),
      React.createElement(CounterView, null),
    ),
  );
}
