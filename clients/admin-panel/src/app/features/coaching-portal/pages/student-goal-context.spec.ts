import { contextExamCodes } from './student-goal-context';
describe('student goal context',()=>{
  it('offers only LGS for LGS and TYT plus the chosen YKS field',()=>{
    expect(contextExamCodes({purpose:'Lgs'} as any)).toEqual(['LGS']);
    expect(contextExamCodes({purpose:'Yks',primaryScoreType:'SAY'} as any)).toEqual(['TYT','AYT_SAY']);
    expect(contextExamCodes({purpose:'Yks',primaryScoreType:'YDT'} as any)).toEqual(['TYT','YDT_EN']);
  });
  it('treats the profile language field DIL as the YDT exam of net targets',()=>{
    expect(contextExamCodes({purpose:'Yks',primaryScoreType:'DIL'} as any)).toEqual(['TYT','YDT_EN']);
  });
  // E7 review (PR 120): the goal chooser left the student UI with goalFitsContext; only the exam codes remain.
  it('has no goal-chooser helpers any more', async ()=>{
    expect(Object.keys(await import('./student-goal-context'))).toEqual(['contextExamCodes']);
  });
});
