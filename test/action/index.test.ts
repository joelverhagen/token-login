import * as main from '../../src/action/main'

const runMock = jest.spyOn(main, 'run').mockImplementation()

describe('index', () => {
  it('calls run when imported', async () => {
    require('../../src/action/index')

    expect(runMock).toHaveBeenCalled()
  })
})
