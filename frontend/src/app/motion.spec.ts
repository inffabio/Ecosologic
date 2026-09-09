import { canAnimate } from './motion';

describe('motion preferences', () => {
  it('disables scripted motion when reduced motion is preferred', () => {
    expect(canAnimate(true)).toBeFalse();
  });

  it('allows scripted motion by default', () => {
    expect(canAnimate(false)).toBeTrue();
  });
});
